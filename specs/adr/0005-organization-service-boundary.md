# ADR 0005 — Service Organization autonome

- Statut : accepté
- Date : 18 juillet 2026

## Contexte

La configuration publiée décrit déjà une hiérarchie d'unités et des affectations de contenu. Ces objets restent toutefois des paramètres de conception : ils ne portent ni memberships actifs, ni encadrement, ni historique opérationnel, et ne peuvent donc pas servir de frontière d'isolation à l'exécution.

Les écoles, entreprises et organismes de formation ont besoin d'une source de vérité transactionnelle pour savoir qui appartient à quel front et à quelle unité, qui encadre qui, et quel contenu est réellement affecté à un joueur pendant une fenêtre donnée.

## Décision

Créer un service autonome `Organization`, avec ses projets Domain, Application, Infrastructure et Api et sa propre base PostgreSQL.

`Organization` possède :

- les fronts opérationnels et leurs unités hiérarchiques ;
- les memberships participant/encadrant avec période de validité ;
- les affectations de parcours, catégorie ou scénario à une unité ;
- la résolution d'accès d'un utilisateur à un front et à son catalogue affecté.

`Configuration` conserve le branding, la terminologie et les modèles publiables. Une synchronisation explicite pourra initialiser une organisation depuis une configuration publiée, mais aucun des deux services ne lit la base de l'autre.

`Identity` continue de posséder comptes, rôles et permissions. Ses jetons exposent les portées signées nécessaires aux services propriétaires. `Organization` combine permission et portée de front avec la ressource demandée ; un contrôle visuel dans un client ne vaut jamais autorisation.

## Conséquences

- une affectation configurable ne devient opérationnelle qu'après création dans `Organization` ;
- les appels internes de résolution sont idempotents, authentifiés par la clé interne et ne transportent que des identifiants stables ;
- aucun rôle métier (`teacher`, `manager`) n'est codé en dur : le membership distingue seulement `Participant` et `Supervisor`, tandis que les actions restent protégées par permissions ;
- chaque requête est filtrée par `frontId` et les tests doivent prouver l'isolation croisée ;
- l'ajout du service impose une image, une base, des health checks, des migrations, l'observabilité et une entrée Compose dédiées.

## Alternatives rejetées

- Ajouter les memberships au document JSON de Configuration : absence de transactions adaptées aux volumes et mélange entre design publié et exploitation quotidienne.
- Ajouter les tables dans Identity : les comptes ne doivent pas posséder les structures pédagogiques ni les affectations de contenu.
- Ajouter les tables dans Play : les sessions ne doivent pas devenir le référentiel des organisations.
