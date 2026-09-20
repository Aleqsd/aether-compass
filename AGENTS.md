# Aether Compass

Plugin Dalamud API 15, .NET 10, interface française. Analyse locale en lecture seule du personnage connecté. Ne jamais lancer une action de jeu ni déduire les quêtes/weekly d'un autre joueur.

- `src/Core` : modèle pur, catalogue sourcé et moteur. `GameSnapshotReader.cs` : lecture du client. `MainWindow.cs` : UI. `Plugin.cs` : cycle de vie.
- Direction utilisateur : style LMeter, pas de fenêtre de configuration classique. Fond anthracite, pas de barre de titre native, en-tête intégré draggable, contrôles repli/verrouillage/préférences/fermeture et lignes compactes. Thème dans `CompassTheme.cs`, détails au clic, contenu défilant séparément. Verrouiller ne doit jamais désactiver les boutons.
- Unknown est distinct de No. Une réussite de raid n'est pas une récompense de butin. Stock de monnaie et acquisition hebdomadaire sont distincts.
- Revalider les limites par patch. Garder les sources et la date dans le catalogue ; les anciens paliers ne sont pas une preuve de l'état actuel.
- Pas d'IO ni de lecture native dans Draw. Données et progression privées restent dans la configuration Dalamud, séparées par ContentId ; aucune télémétrie.
- `build.ps1` compile, exécute les tests métier et prépare le paquet. Tests de reset en UTC et d'isolation par personnage obligatoires pour toute modification de progression.
- Une compilation ou un rendu hors jeu ne prouve pas le chargement en jeu. Publier en prérelease tant que la matrice `docs/VALIDATION.md` n'a pas été exécutée en jeu.
- Aucune soumission automatique au catalogue officiel Dalamud. L'intégration au dépôt personnalisé d'Aleqsd est une opération séparée.
