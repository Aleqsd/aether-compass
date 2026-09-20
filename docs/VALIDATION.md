# Validation de la préversion 0.2.0

Le 20 septembre 2026, compilation Release contre les bibliothèques officielles Dalamud **15.0.3.5**, SDK .NET **10.0.401**. Pas de session FFXIV disponible pour une validation réelle du chargement.

## Vérifié hors jeu

- Compilation du plugin et de son moteur sans erreur ni avertissement.
- **64/64 scénarios métier réussis** : frontières exactes des resets UTC, fuseaux et heure d'été, observations anciennes, acquisitions hebdomadaires indépendantes du stock, états inconnus, priorité des lectures sur le manuel, sauvegarde par personnage, accès de chaque étage et récompenses comparées aux emplacements correspondants.
- Observation de récompenses : amorçage sans compter les anciennes possessions, gain net confirmé, piles, mouvements/split/merge/équipement à bilan nul, retrait puis retour, duplication transitoire, événement absent ou objet sans rapport, changement de personnage/territoire, inventaire indisponible, déconnexion, interruption et reset pendant une acquisition. Preuves séparées, idempotentes, persistantes, datées ; compatibilité avec les sauvegardes v1 sans preuve.
- Packaging : versions manifeste/assembly identiques, DLL de moteur incluse, SHA256 du ZIP et des DLL produits par le script.

Les sorties du script `build.ps1` sont la preuve reproductible. Les aperçus de `tools/Preview` rendent les listes de dessin ImGui du véritable panneau avec des données fictives ; ils ne sont pas des captures FFXIV.

Neuf aperçus inspectés : objectifs niveau 100 i762, suivi hebdomadaire inconnu, personnage déconnecté, largeur minimale 520 à 150 %, objectif déplié, en-tête replié, récompenses observées, leur variante à 150 % et leur infobulle de provenance. Aucun débordement horizontal constaté ; le défilement vertical conserve l'accès aux objectifs suivants.

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
7. Dans Windurst : acquérir seulement une armure, seulement la pièce, les deux, puis terminer sans acquisition nouvelle. Vérifier que chaque état et chaque objet de provenance correspondent à la récompense réellement reçue. Tester un gain sur pile existante pour Ranperre Coin.
8. Dans M4 normal : acquérir Heavy Holoblade sur pile existante puis rejouer sans droit restant. Tester aussi en cinématique et panneau fermé. M4 Savage et un autre contenu ne doivent jamais déclencher cette preuve.
9. Dans les deux raids : déplacer, équiper, diviser et fusionner d'anciennes récompenses. Activer le plugin avec des objets déjà possédés, se reconnecter et changer de personnage. Aucune fausse acquisition ne doit être créée. Vérifier la présence des 35 noms d'armures et deux jetons sur clients FR/EN ; toute absence doit apparaître dans l'état de suivi.
10. Relancer le plugin après acquisition, vérifier preuve et horodatage ; traverser le reset mardi 08:00 UTC connecté et déconnecté. Une preuve de la semaine passée ne doit plus cocher l'objectif. Vérifier également les déclarations manuelles conservées après mise à jour 0.1.0 → 0.2.0.
11. Redimensionner le panneau, utiliser une échelle d'interface élevée, changer les préférences et vérifier leur sauvegarde.

## Limites connues

- Pas d'observation d'un autre joueur ; pas de scraping Lodestone ni FFLogs.
- Récompenses antérieures à l'observation, acquisitions manquées pendant un chargement ou une interruption et carnet de Khloe : confirmation manuelle requise. Une absence de preuve reste inconnue.
- Pas de simulation BiS, matières, budget marché, maîtrise du combat ou composition de groupe.
- Le catalogue contient des routes représentatives de fin de jeu, pas chaque quête ni chaque activité du jeu. Les étapes exactes de relique et matériaux restent dans le journal.
- Pas de détection automatique exhaustive d'un nouveau patch : sources datées et revalidation nécessaire.
