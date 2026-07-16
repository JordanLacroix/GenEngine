# Gouvernance GitHub et chaîne CI/CD

Ce document inventorie les protections réellement actives. Toute modification d’un workflow, d’une règle de dépôt ou d’un outil de sécurité doit le mettre à jour.

## Principes

- permissions GitHub Actions minimales et explicites ;
- actions tierces épinglées par SHA complet ;
- aucun secret longue durée lorsqu’OIDC est disponible ;
- contrôles complémentaires, sans empiler des scanners redondants ;
- PR obligatoire vers `main`, historique linéaire et fusion par squash ;
- formulaires structurés pour une exploitation fiable par les humains et les agents IA.

## Fonctionnalités GitHub activées

| Fonctionnalité | Rôle | Coût public |
|---|---|---|
| Issues et formulaires | Bugs et demandes cadrés | Gratuit |
| Discussions | Questions et idées non encore actionnables | Gratuit |
| Pull requests et CODEOWNERS | Revue, traçabilité et ownership | Gratuit |
| Ruleset de `main` | PR, checks, historique linéaire, anti-force-push | Gratuit pour dépôt public |
| Actions | CI/CD et automatisations | Quota GitHub public |
| CodeQL | Analyse statique C# et SARIF | Gratuit pour dépôt public |
| Dependency Review | Bloque dépendances vulnérables ou licences refusées | Gratuit pour dépôt public |
| Dependabot | Alertes, correctifs et mises à jour groupées | Gratuit |
| Secret scanning et push protection | Détection de secrets avant/après push | Gratuit pour dépôt public |
| Private vulnerability reporting | Divulgation coordonnée | Gratuit |
| Releases | Notes de version préparées automatiquement | Gratuit |

## Contrôles automatisés

| Workflow | Déclenchement | Contrôles / outils |
|---|---|---|
| `ci.yml` | PR et `main` | Restore verrouillé, build strict, tests, couverture Codecov OIDC |
| `codeql.yml` | PR, `main`, hebdomadaire | CodeQL C# avec requêtes `security-extended` |
| `dependency-review.yml` | PR | Vulnérabilités ≥ modérées et licences à copyleft fort |
| `security-scan.yml` | PR, `main`, hebdomadaire | Trivy, SARIF, SBOM CycloneDX Anchore |
| `scorecard.yml` | `main`, hebdomadaire | OpenSSF Scorecard et SARIF |
| `docs.yml` | PR et `main` | markdownlint et Lychee |
| `workflow-security.yml` | Changements `.github` | actionlint et zizmor |
| `pr-policy.yml` | PR | Titre conforme aux Conventional Commits |
| `labeler.yml` | PR | Labels de zone selon les chemins |
| `release-drafter.yml` | Fusion et `main` | Brouillon de release et version sémantique par labels |
| `welcome.yml` | Première issue ou PR | Accueil des nouveaux contributeurs |

StepSecurity Harden Runner observe les sorties réseau de chaque job. Toutes les actions sont épinglées ; Dependabot propose leurs mises à jour.

## Outils externes

| Outil | État | Décision |
|---|---|---|
| Codecov | Actif dès qu’un rapport existe | Couverture via OIDC, aucun token permanent |
| SonarCloud | Différé | Compte externe et recouvrement partiel avec CodeQL ; à activer si les quality gates deviennent utiles |
| Snyk | Différé | Compte externe et recouvrement avec Dependabot, Dependency Review et Trivy |
| FOSSA | Différé | À évaluer si un inventaire juridique avancé des licences devient nécessaire |
| Renovate | Non retenu | Dependabot couvre déjà NuGet et GitHub Actions |
| Semgrep | Non retenu pour l’instant | CodeQL fournit le SAST principal |
| Gitleaks | Non retenu pour l’instant | Secret scanning et push protection couvrent ce besoin sur GitHub public |

Ajouter un outil exige un besoin non couvert, une politique de rétention acceptable, des permissions minimales et une procédure de retrait.

## Politique de fusion

Le ruleset de `main` impose une pull request, la résolution des conversations, les checks requis, l’historique linéaire et interdit suppression ou force-push. Aucun avis obligatoire n’est imposé tant que le projet n’a qu’un mainteneur ; ce seuil doit passer à un dès qu’un second reviewer régulier est disponible.

Les fusions se font par squash. Le titre de PR devient le sujet du commit et doit respecter les Conventional Commits.

## Maintenance

Chaque mois, ou avant une release :

1. traiter les alertes Dependabot, CodeQL, Trivy et Scorecard ;
2. examiner les permissions et actions épinglées ;
3. vérifier les checks requis et les labels ;
4. mettre à jour ce document, le README et `SECURITY.md` si le dispositif change ;
5. publier ou supprimer les brouillons de release obsolètes.
