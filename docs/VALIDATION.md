# Validation de la préversion 0.1.0

Le 20 septembre 2026, compilation Release contre les bibliothèques officielles Dalamud **15.0.3.5**, SDK .NET **10.0.401**. Pas de session FFXIV disponible pour une validation réelle du chargement.

## Vérifié hors jeu

- Compilation du plugin et de son moteur sans erreur ni avertissement.
- **37/37 scénarios métier réussis** : frontières exactes des resets UTC, fuseaux et heure d'été, observations anciennes, acquisitions hebdomadaires indépendantes du stock, états inconnus, priorité des lectures sur le manuel, sauvegarde par personnage, accès de chaque étage et récompenses comparées aux emplacements correspondants.
- Packaging : versions manifeste/assembly identiques, DLL de moteur incluse, SHA256 du ZIP et des DLL produits par le script.

Les sorties du script `build.ps1` sont la preuve reproductible. Les aperçus de `tools/Preview` rendent les listes de dessin ImGui du véritable panneau avec des données fictives ; ils ne sont pas des captures FFXIV.

Six aperçus inspectés : objectifs niveau 100 i762, suivi hebdomadaire inconnu, personnage déconnecté, largeur minimale 520 à 150 %, objectif déplié et en-tête replié. Aucun débordement horizontal constaté ; le défilement vertical conserve l'accès aux objectifs suivants.

Le harnais simule aussi les clics ImGui : ouverture d'une ligne, verrouillage avec sauvegarde, repli, restauration de la hauteur précédente et fermeture lorsque le panneau est à la fois verrouillé et replié. Ces interactions passent hors jeu. Le panneau utilise un en-tête personnalisé et un thème inspiré de LMeter.

```powershell
dotnet build tools/Preview/Preview.csproj -c Release "-p:DalamudHome=<dossier Dalamud>"
dotnet tools/Preview/bin/Release/net10.0-windows/Preview.dll "<dossier Dalamud>" "."
```

## À vérifier en jeu

1. Chargement et déchargement du plugin via Dev Plugin Locations ; ouvrir et fermer par commande et boutons Dalamud.
2. Personnage niveau 100, changement de job, niveau synchronisé et sortie d'instance. Le niveau réel doit rester correct.
3. Changement de zone, cinématique, combat et déconnexion : aucune ancienne observation ne doit rester présentée comme active.
4. Quêtes et accès connus/verrouillés, dont les quatre étages Heavyweight. Vérifier les noms résolus avec un client français et anglais.
5. Comparer l'iLvl moyen et chaque emplacement avec la fenêtre personnage, y compris arme à deux mains, bouclier de paladin et emplacement vide.
6. Comparer monnaie gagnée et stock avant/après dépense. Comparer le bonus Expert avec le menu des missions.
7. Vérifier les déclarations hebdomadaires avant/après reset, reconnexion et changement de personnage ; un clear sans butin ne doit pas cocher la pièce d'alliance.
8. Redimensionner le panneau, utiliser une échelle d'interface élevée, changer les préférences et vérifier leur sauvegarde.

## Limites connues

- Pas d'observation d'un autre joueur ; pas de scraping Lodestone ni FFLogs.
- Droits de butin d'alliance, Heavy Holoblade et carnet de Khloe déclaratifs lorsqu'aucune lecture native fiable n'est disponible.
- Pas de simulation BiS, matières, budget marché, maîtrise du combat ou composition de groupe.
- Le catalogue contient des routes représentatives de fin de jeu, pas chaque quête ni chaque activité du jeu. Les étapes exactes de relique et matériaux restent dans le journal.
- Pas de détection automatique exhaustive d'un nouveau patch : sources datées et revalidation nécessaire.
