# Aether Compass

**Choisir son prochain objectif en Éorzéa.** Plugin Dalamud en français, conçu pour les nouveaux niveaux 100, le rattrapage d'équipement et la progression en fin de jeu.

Aether Compass croise le niveau réel du job, les pièces équipées, les quêtes d'épopée, les accès aux contenus et le plafond hebdomadaire de mémoquartz. Il propose des objectifs classés et explique leurs prérequis, leur intérêt et les données qui restent à vérifier.

Le panneau s'inspire de **LMeter** : fond anthracite translucide, en-tête intégré, accent discret et lignes compactes. Les six premières priorités sont visibles en lecture rapide ; cliquer sur une ligne affiche ses raisons et prérequis. L'en-tête permet de déplacer, replier, verrouiller et fermer le panneau. L'engrenage ouvre les préférences dans le même panneau.

**Version 0.2.0 expérimentale — Dalamud API 15, catalogue patch 7.56.** Compilation et tests hors jeu ; comportement en jeu à confirmer. L'analyse porte sur le personnage connecté. Les quêtes, monnaies et weekly des autres joueurs ne sont pas accessibles à ce plugin.

![Objectifs expliqués pour un personnage niveau 100](docs/images/goals-level100.png)

*Interface ImGui réelle rendue hors jeu, avec un personnage fictif. Ce n'est pas une capture FFXIV.*

## Ce que propose le plugin

- Objectifs adaptés au palier : épopée, rattrapage i750–770, donjons Expert, Heavyweight normal, alliance Windurst, Extrême, Savage ou relique.
- Lecture du niveau réel du job, même lorsque son acteur est synchronisé en instance.
- Comparaison des récompenses avec les emplacements concernés : une arme ne corrige pas une bague faible. L'iLvl ne remplace pas un calcul de statistiques ou de BiS.
- Suivi distinct des mémoquartz gagnés cette semaine et du stock disponible.
- Récompenses d'alliance suivies séparément : équipement et pièce d'échange. La Heavy Holoblade normale possède aussi son suivi propre.
- Suivi automatique des acquisitions de **Heavy Holoblade**, **Ranperre Coin** et des **35 armures de Windurst** : un événement d'inventaire doit correspondre à un gain net confirmé dans l'instance exacte. Un clear ou une ancienne pièce possédée ne suffit pas.
- États **fait**, **à faire**, **inconnu**, avec provenance visible : compteur du jeu, récompense observée ou déclaration manuelle. Le survol d'une récompense observée montre l'objet reçu, le contenu et l'heure.
- Preuves et déclarations sauvegardées séparément par personnage. Les activités hebdomadaires expirent au mardi 08:00 UTC ; les quotidiennes à 15:00 UTC. Au nouveau cycle, une preuve passée ne coche plus l'objectif.
- Préférences de difficulté, temps de session, priorité équipement/histoire/hebdomadaire et carnet de Khloe facultatif.

L'interface reste fermée au chargement. Ouvrir et déplier avec **`/goals`** ou **`/aethercompass`** ; **`/aethercompass weekly`** ouvre le suivi hebdomadaire. Le verrouillage empêche seulement le déplacement et le redimensionnement ; les boutons restent actifs. L'analyse du personnage est suspendue en combat et en cinématique ; l'observation des récompenses reste active dans les deux raids couverts, même avec le panneau fermé.

## Installer la préversion

1. Télécharger et extraire le ZIP de la [préversion](https://github.com/Aleqsd/aether-compass/releases).
2. Conserver ensemble `AetherCompass.dll`, `AetherCompass.Core.dll`, `AetherCompass.deps.json` et `AetherCompass.json`.
3. Dans les réglages Dalamud, onglet expérimental, ajouter le **chemin complet de `AetherCompass.dll`** aux *Dev Plugin Locations*.
4. Activer Aether Compass dans les plugins de développement, puis saisir `/goals`.

Le dépôt personnalisé commun d'Aleqsd n'est pas modifié par cette première préversion. Elle n'est pas soumise au catalogue officiel Dalamud.

![Suivi hebdomadaire avec provenance des récompenses](docs/images/weekly-observed.png)

*Aperçu fictif : récompenses observées, déclaration manuelle et état inconnu sont distingués.*

L'observation commence après chargement et stabilisation des inventaires. Les récompenses obtenues avant l'activation du plugin, pendant une interruption de lecture ou à la sortie d'une instance ne sont pas reconstituées. Sans preuve, l'état reste **inconnu** et peut être renseigné manuellement. Le carnet de Khloe reste déclaratif.

## Compiler et vérifier

Installer le SDK .NET 10 et disposer des bibliothèques Dalamud API 15, puis :

```powershell
./build.ps1 -DalamudHome "$env:APPDATA/XIVLauncher/addon/Hooks/dev"
```

Le script compile le plugin, exécute les tests métier et crée `releases/AetherCompass-0.2.0.zip` avec ses empreintes. Accepte aussi `-Dotnet` pour un SDK portable.

Les tests du moteur n'ont pas besoin du jeu ou de Dalamud :

```powershell
dotnet run --project tests/AetherCompass.Core.Tests.csproj -c Release
```

## Données et maintenance

Aucun service distant, compte supplémentaire ou clé API. Le plugin ne lance aucune action en jeu. Les préférences, déclarations et preuves de récompenses restent dans la configuration locale Dalamud. Un fichier de progression illisible est conservé ; les nouvelles modifications utilisent un fichier de récupération séparé. La progression de la version 0.1.0 est conservée à la mise à jour.

Les paliers et restrictions sont versionnés dans `src/Core/ObjectiveCatalog.cs`. Ils ont été vérifiés dans les notes officielles 7.4 à 7.56 ; il faut les revalider après les mises à jour. La lecture dynamique du plafond et des accès n'actualise pas à elle seule toutes les règles de récompense.

[Sources du contenu](docs/CONTENT-SOURCES.md) · [Couverture des données](docs/DATA-COVERAGE.md) · [Validation et limites](docs/VALIDATION.md)
