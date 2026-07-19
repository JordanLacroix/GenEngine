# Tâches — Configuration et Organization

## Configuration

| ID | Statut | Tâche |
|---|---|---|
| CFG-001 | done | Décider par ADR les frontières et contrats de Configuration et Organization |
| CFG-002 | in-progress | Déclarer un registre de paramètres typés avec schéma, défaut, portée et validation |
| CFG-003 | todo | Résoudre les valeurs plateforme → front → catégorie/parcours → groupe → utilisateur |
| CFG-004 | in-progress | Versionner, publier et auditer les changements de configuration |
| CFG-005 | in-progress | Gérer feature flags et activation de modules sans contourner le RBAC |
| CFG-006 | done | Séparer références de secrets et paramètres administrables |
| CFG-007 | todo | Importer/exporter une configuration portable et validée |
| CFG-008 | in-progress | Exposer configuration effective, provenance et capacités aux clients |
| CFG-009 | in-progress | Configurer branding, locale, fuseau, terminologie, calendrier et auth par front |
| CFG-010 | todo | Publier le catalogue typé complet des clés moteur et plateforme |
| CFG-011 | todo | Gérer brouillon, diff, approbation, planification, rollback et provenance |

## Contenu de référence — Diapason

Bible d'univers et documentation : [`specs/domain/diapason/`](../../domain/diapason/). Contenu jouable : [`content/diapason/`](../../../content/diapason/).

| ID | Statut | Tâche |
|---|---|---|
| DIA-001 | done | Écrire la bible d'univers, le ton, la position du joueur et le lien à la grammaire visuelle/sonore du POC |
| DIA-002 | done | Définir une taxonomie par posture (six catégories) et la justifier |
| DIA-003 | done | Écrire dix scénarios ancrés en 2026, avec fins de rupture imposant une reprise |
| DIA-004 | done | Exprimer la progression par trois parcours et leurs `PrerequisiteJourneyIds` |
| DIA-005 | done | Amorcer Diapason comme configuration de référence à la première initialisation |
| DIA-006 | done | Fournir un script d'installation du contenu jouable via les API publiques |
| DIA-007 | done | Documenter la rotation quotidienne déterministe et la couvrir par un test |
| DIA-008 | todo | Exposer la rotation quotidienne dans le modèle de configuration (`SpotlightDefinition`) — nécessite une décision produit |
| DIA-009 | todo | Rendre `install-diapason.sh` idempotent (Authoring crée un brouillon à chaque import) |
| DIA-010 | todo | Distinguer une fin d'échec d'une fin de réussite (`outcome` sur le nœud terminal) — bloque récompenses et projections |
| DIA-011 | todo | Déclarer et valider les `scope` de `recordNotableEvent` côté configuration |

## Organization

| ID | Statut | Tâche |
|---|---|---|
| ORG-001 | done | Rattacher un profil School/Company/Training/Community/Custom au front |
| ORG-002 | done | Gérer années, semestres ou périodes configurables |
| ORG-003 | done | Gérer classes/groupes hiérarchiques et memberships encadrants/participants |
| ORG-004 | done | Inscrire les utilisateurs avec validité temporelle, historique par période et import de masse prévalidé/idempotent |
| ORG-005 | done | Affecter scénario, catégorie ou parcours à une unité avec fenêtre et échéance, l'appliquer dans Play et filtrer les catalogues clients |
| ORG-006 | in-progress | Tester l'isolation stricte entre fronts et les refus Play ; généralisation aux autres services restante |
| ORG-007 | done | Exposer une vue établissement → période → classe → membres sans données excessives |
| ORG-008 | todo | Configurer tentatives, aide, réussite, reprise et visibilité des résultats par groupe |
| ORG-009 | done | Supporter départements/équipes/cohortes et managers via unités et memberships sans vocabulaire imposé |
| ORG-010 | done | Configurer parcours, relation N-N aux catégories et rattachement explicite de scénarios |
| ORG-011 | done | Configurer fenêtres et échéances, résoudre memberships actifs et appliquer l'accès runtime scénario/catégorie |
