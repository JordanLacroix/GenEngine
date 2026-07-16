# shellcheck shell=bash
# Shared age encryption/decryption helpers for the database backup tooling.
#
# Two modes are supported, selected automatically from the environment:
#
#   * Passphrase (symmetric) mode  -- local development.
#     Enabled when BACKUP_AGE_PASSPHRASE is set. The passphrase never touches
#     the command line and is fed to age through a pseudo-terminal, because the
#     age CLI reads passphrases only from a controlling terminal.
#
#   * Recipient (asymmetric) mode  -- production / shared environments.
#     Enabled when BACKUP_AGE_RECIPIENTS or BACKUP_AGE_RECIPIENTS_FILE is set
#     for encryption, and BACKUP_AGE_IDENTITY_FILE for decryption. This uses
#     age recipients (public keys) and identity (private key) files. No secret
#     is ever passed on the command line or stored in this repository.
#
# This file is meant to be sourced, not executed. It defines:
#   age_require_tool          fail early if `age` is missing
#   age_mode                  echo "passphrase" or "recipient" (encryption)
#   age_encrypt <out>         encrypt stdin -> file <out>
#   age_decrypt <in> <out>    decrypt file <in> -> file <out> ("-" == stdout)

set -o pipefail

age_require_tool() {
  if ! command -v age >/dev/null 2>&1; then
    cat >&2 <<'EOF'
error: `age` is required but was not found on PATH.

Install it:
  macOS   : brew install age
  Debian  : sudo apt-get install age
  Other   : https://github.com/FiloSottile/age/releases

age encrypts every database dump. Backups are refused without it.
EOF
    return 127
  fi
}

# Determine the encryption mode from the environment. Prints "passphrase" or
# "recipient", or fails if neither is configured.
age_mode() {
  if [ -n "${BACKUP_AGE_PASSPHRASE:-}" ]; then
    echo passphrase
  elif [ -n "${BACKUP_AGE_RECIPIENTS:-}" ] || [ -n "${BACKUP_AGE_RECIPIENTS_FILE:-}" ]; then
    echo recipient
  else
    cat >&2 <<'EOF'
error: no age encryption configuration found.

Local development (symmetric passphrase):
  export BACKUP_AGE_PASSPHRASE='a-strong-dev-passphrase'

Production (asymmetric recipients):
  export BACKUP_AGE_RECIPIENTS='age1...'          # one or more, comma/space separated
  # or
  export BACKUP_AGE_RECIPIENTS_FILE=/path/to/recipients.txt
EOF
    return 1
  fi
}

# Embedded Python helper. age reads passphrases exclusively from a controlling
# terminal (never from a pipe), so passphrase mode drives age through a PTY for
# the prompts while keeping plaintext on a real stdin pipe and ciphertext on a
# real stdout pipe. Requires python3 (already used by this repo's tooling).
_age_pty_py() {
  cat <<'PY'
import os, sys, fcntl, termios, select, time, signal

def die(msg):
    sys.stderr.write("age(pty): " + msg + "\n")
    os._exit(1)

signal.signal(signal.SIGALRM, lambda *a: die("timed out talking to age"))
signal.alarm(int(os.environ.get("AGE_PTY_TIMEOUT", "120")))

pp = os.environ["BACKUP_AGE_PASSPHRASE"].encode()
mode, infile, outfile = sys.argv[1], sys.argv[2], sys.argv[3]
argv = ["age", "-p"] if mode == "encrypt" else ["age", "-d"]

data = sys.stdin.buffer.read() if infile == "-" else open(infile, "rb").read()

tty_master, tty_slave = os.openpty()
in_r, in_w = os.pipe()
out_r, out_w = os.pipe()
pid = os.fork()
if pid == 0:
    os.setsid()
    fcntl.ioctl(tty_slave, termios.TIOCSCTTY, 0)
    os.dup2(in_r, 0); os.dup2(out_w, 1); os.dup2(tty_slave, 2)
    for fd in (tty_master, tty_slave, in_r, in_w, out_r, out_w):
        try: os.close(fd)
        except OSError: pass
    os.execvp(argv[0], argv)
    os._exit(127)

os.close(tty_slave); os.close(in_r); os.close(out_w)
os.set_blocking(tty_master, False)

def wait_prompt():
    seen = b""; end = time.time() + 10
    while time.time() < end:
        r, _, _ = select.select([tty_master], [], [], 0.1)
        if r:
            try: seen += os.read(tty_master, 4096)
            except OSError: pass
            if seen.rstrip().endswith(b":"):
                return

def send_pass():
    # age flushes pending tty input (TCSAFLUSH) right before reading the
    # passphrase; wait for the prompt, then a beat, so our bytes survive.
    wait_prompt(); time.sleep(0.3); os.write(tty_master, pp + b"\n")

send_pass()
if mode == "encrypt":
    send_pass()  # age asks a second time to confirm

os.write(in_w, data); os.close(in_w)

out = open(sys.stdout.fileno(), "wb", closefd=False) if outfile == "-" else open(outfile, "wb")
while True:
    r, _, _ = select.select([out_r, tty_master], [], [], 10)
    if not r:
        break
    if out_r in r:
        try: chunk = os.read(out_r, 65536)
        except OSError: chunk = b""
        if not chunk:
            break
        out.write(chunk)
    if tty_master in r:
        try: os.read(tty_master, 4096)
        except OSError: pass
out.flush()
if outfile != "-":
    out.close()
os.close(out_r)
_, status = os.waitpid(pid, 0)
ok = os.WIFEXITED(status) and os.WEXITSTATUS(status) == 0
sys.exit(0 if ok else 1)
PY
}

# Build the age recipient arguments for encryption from the environment.
_age_recipient_args() {
  local -a args=()
  if [ -n "${BACKUP_AGE_RECIPIENTS_FILE:-}" ]; then
    args+=(-R "$BACKUP_AGE_RECIPIENTS_FILE")
  fi
  if [ -n "${BACKUP_AGE_RECIPIENTS:-}" ]; then
    local recipient
    for recipient in ${BACKUP_AGE_RECIPIENTS//,/ }; do
      args+=(-r "$recipient")
    done
  fi
  printf '%s\n' "${args[@]}"
}

# Encrypt stdin to the file given as $1.
age_encrypt() {
  local out="$1" mode
  mode="$(age_mode)" || return 1
  if [ "$mode" = passphrase ]; then
    python3 -c "$(_age_pty_py)" encrypt - "$out"
  else
    local -a rargs=()
    mapfile -t rargs < <(_age_recipient_args)
    age "${rargs[@]}" -o "$out"
  fi
}

# Decrypt file $1 to file $2 ("-" means stdout).
age_decrypt() {
  local in="$1" out="$2"
  if [ -n "${BACKUP_AGE_PASSPHRASE:-}" ]; then
    python3 -c "$(_age_pty_py)" decrypt "$in" "$out"
  elif [ -n "${BACKUP_AGE_IDENTITY_FILE:-}" ]; then
    if [ "$out" = "-" ]; then
      age -d -i "$BACKUP_AGE_IDENTITY_FILE" "$in"
    else
      age -d -i "$BACKUP_AGE_IDENTITY_FILE" -o "$out" "$in"
    fi
  else
    cat >&2 <<'EOF'
error: no age decryption configuration found.

Local development (symmetric passphrase):
  export BACKUP_AGE_PASSPHRASE='the-passphrase-used-at-backup-time'

Production (asymmetric identity / private key):
  export BACKUP_AGE_IDENTITY_FILE=/path/to/key.txt
EOF
    return 1
  fi
}
