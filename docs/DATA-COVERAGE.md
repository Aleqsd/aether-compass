# Données observées et limites

Le lecteur utilise **Dalamud API 15** et ses données Lumina locales. Il observe uniquement le personnage connecté, identifié par son `ContentId`. Aucune requête réseau, inspection de joueur ciblé, commande de jeu, écriture mémoire ou inscription automatique en instance n'est nécessaire.

## Couverture

| Information | Source | Interprétation |
|---|---|---|
| Identité, job, niveau | `IPlayerState`, validé avec la connexion et le personnage local | `Level` est le niveau réel, distinct du niveau synchronisé `EffectiveLevel`. |
| Niveau d'objet moyen | `UIState.CurrentItemLevel` | Valeur de la fiche de personnage ; une valeur absente reste inconnue. |
| Équipement | Inventaire `EquippedItems` chargé + table `Item` | Arme, bouclier lorsqu'il s'applique, armure et accessoires. Ceinture et cristal de job exclus. Les emplacements effectivement vides valent zéro ; les données manquantes sont omises. |
| Quêtes | Table `Quest` + `IUnlockState.IsQuestCompleted` | Fin de Dawntrail, MSQ `Windborne`, accès aux armes fantômes et à Khloe. |
| Instances | Table `ContentFinderCondition.Content`, résolue vers une vraie ligne `InstanceContent` + `IUnlockState` | Aucun identifiant de `ContentFinderCondition` n'est passé à une fonction qui attend `InstanceContent`. |
| Accès Expert | Membres actuels marqués `ExpertRoulette` dans les données du client + premier accomplissement | Il faut avoir terminé les donjons de la liste, pas seulement les avoir débloqués. |
| Bonus Expert du jour | Roulette identifiée par nom exact dans `ContentRoulette`, puis `IsRouletteComplete` | Suivi **quotidien**, même si le dictionnaire technique s'appelle `Weekly`. |
| Mémoquartz limités acquis | `GetWeeklyAcquiredTomestoneCount` | Compteur acquis depuis le reset, jamais déduit du stock. |
| Plafond hebdomadaire | `GetLimitedTomestoneWeeklyLimit` | Le plafond vient du client ; il n'est pas figé à 450. |
| Stock de mémoquartz limités | Monnaie unique identifiée par `TomestonesItem`/`Tomestones.WeeklyLimit`, puis `GetTomestoneCount` | Si la monnaie n'est pas identifiée sans ambiguïté, le stock reste inconnu. |
| Hololame Heavyweight | `IGameInventory.InventoryChanged` + gain net de Heavy Holoblade dans AAC Heavyweight M4 normal | Preuve d'acquisition observée, datée et conservée pour la semaine du personnage. |
| Pièce de Windurst | Même observation pour Ranperre Coin dans Windurst | Indépendante du butin d'équipement. |
| Équipement de Windurst | Même observation pour les 35 armures Vana'dielian de la liste officielle | Objet identifié par nom exact, niveau 100, i780, non échangeable et emplacement d'armure ; jamais par iLv seul. |

Les accès au raid normal M1 et M4 sont distincts. Chaque étage Savage possède son propre état d'accès. L'accès à `A Phantom Reborn` indique le début de la progression de relique ; il ne prouve pas l'étape courante d'une arme.

## Ce qui demande une confirmation dans l'interface

Les droits aux récompenses ne sont pas déduits d'un clear ou d'un objet déjà présent en inventaire. Depuis la version 0.2.0, l'acquisition de l'hololame Heavyweight, de la pièce de Windurst et de son équipement peut être constatée pendant que le plugin fonctionne. Sans cette preuve, une lecture native connue ou une déclaration manuelle du cycle, l'état reste **inconnu**. L'absence de gain ne prouve jamais qu'une récompense reste disponible.

Le carnet de Khloe possède sa propre validité : posséder un carnet, y poser neuf vignettes et l'avoir rendu cette semaine sont trois états différents. Le lecteur n'assimile aucun de ces états à une remise hebdomadaire.

### Observation des acquisitions depuis la version 0.2.0

`WeeklyRewardObserver` écoute les événements réconciliés `IGameInventory.InventoryChanged`. Un ajout ou une hausse de quantité constitue seulement un candidat. Le moteur pur `RewardInventoryTracker` exige aussi :

1. Le même personnage connecté et le territoire associé au contenu exact, résolu depuis les tables locales. Aucun scan des servants, autres joueurs, coffres ou banques.
2. Les quatre sacs, l'équipement et les armureries personnelles chargés. Tous leurs objets sont agrégés : équiper, déplacer, fusionner ou diviser une pile ne crée pas un gain.
3. Une référence initiale stable pendant deux secondes. Le stock au chargement ou à la reconnexion n'est jamais traité comme une acquisition.
4. Un gain net au-dessus du maximum déjà vu dans cette session, confirmé pendant une seconde. Une baisse puis un retour du même stock, ou une duplication transitoire lors d'un déplacement, n'apporte pas de preuve.

Déconnexion, changement de territoire/personnage, inventaire indisponible, interruption de plus de cinq secondes ou changement de semaine réinitialisent l'observation. Une acquisition pendant l'amorçage, une interruption ou immédiatement avant la sortie peut donc ne pas être reconnue. Le plugin privilégie alors l'état inconnu. Il reste actif en combat et en cinématique pour recevoir les récompenses, indépendamment du panneau et de la lecture périodique du personnage.

Le suivi distingue les deux récompenses de Windurst. Une pièce seule ne coche pas l'équipement et une armure seule ne coche pas la pièce. Un succès de mission n'est jamais utilisé comme preuve de butin ; les messages de chat ne sont pas analysés.

Les preuves conservent l'objet, son identifiant, le contenu, le territoire, la date et la méthode d'observation, séparément des déclarations manuelles. Une seule preuve par objectif est retenue pour la semaine. La priorité est : état natif connu, preuve actuelle, puis déclaration manuelle. Les preuves expirent au mardi 08:00 UTC même si le plugin était arrêté au moment du reset ; elles survivent à la déconnexion et au redémarrage du plugin. Les fichiers de progression 0.1.0 restent lisibles.

### Limites de l'historique et des droits natifs

Une revue des assemblies API 15 et des structures publiées a été effectuée pour la version initiale :

- `PlayerState.WeeklyLockoutInfo` est un champ interne dont la signification des bits n'est pas documentée. Aucun accès publié ne relie un bit à la pièce de Windurst ou à l'hololame Heavyweight.
- `AgentContentsFinderInterface.GetReceivedRewardCount()` et `GetMaxReceivedRewardCount()` renvoient des **totaux** pour le contenu sélectionné dans l'outil de mission. Ils n'identifient pas séparément la pièce et le butin. Les quatre valeurs qui contribuent au maximum sont elles-mêmes indéterminées dans la structure publiée. Le lecteur ne change pas la sélection de l'outil de mission pour tenter de les charger.
- `LootItem.WeeklyLootItem` décrit un objet dans la fenêtre de butin courante. Il ne constitue pas un historique persistant des récompenses du personnage et ne couvre pas une récompense acquise avant le lancement du plugin.
- Les fonctions `WeeklyBingo` publiées renseignent le carnet possédé, ses vignettes et son expiration ; elles ne fournissent pas un état documenté « récompense du carnet remise pendant ce cycle ».

Ces obstacles empêchent de reconstituer les acquisitions antérieures au démarrage avec le contrat actuel. L'observation événementielle ci-dessus apporte seulement des preuves nouvelles. Aucun offset deviné, masque de bits non documenté ou scan de signature personnalisé n'est ajouté. Les cas pièce seule, butin seul, les deux, aucun, puis le reset restent à valider en jeu.

L'analyse d'équipement relève le niveau d'objet et l'éligibilité au job. Elle ne calcule pas le meilleur équipement théorique, les seuils de vitesse, les matéria optimales, la qualité NQ/HQ, les réparations ou un score de maîtrise du joueur. Les autres jobs, les servants et les personnages déconnectés ne sont pas scannés.

## Chargement, changement de personnage et mises à jour

`GameSnapshotReader.Read()` doit être appelé sur le thread de mise à jour Dalamud. Le plugin limite sa fréquence. Aucune fiche du personnage n'est publiée sans personnage local et état joueur chargé, ou pendant le combat, un changement de zone, une déconnexion ou une cinématique. L'observateur de récompenses suit un cycle séparé décrit ci-dessus. L'identité est vérifiée une seconde fois avant de publier le résultat. Aucun pointeur natif n'est conservé entre deux appels.

Les signatures des fonctions utilisées sont vérifiées avant l'appel ; un pointeur, une table ou une donnée indisponible ne devient jamais un état « non fait » par défaut. `KnowledgeState.Unknown` est différent de `No`. Les exceptions ordinaires d'une section la rendent indisponible sans fabriquer de compteur.

Les correspondances quêtes/contenus sont résolues une fois dans les tables **anglaises** du client, indépendamment de la langue de jeu, avec un nom exact et unique. Une correspondance absente ou ambiguë est signalée dans `LastError`. Les identifiants d'instance et de roulette proviennent ainsi du client, pas d'une liste supposée.

Le build technique du jeu est exposé via `GameVersion`. Il s'agit d'un identifiant de build, pas d'un numéro de patch éditorial. Il ne permet pas à lui seul de certifier que le catalogue de récompenses est à jour. Le catalogue de contenu doit être révisé après chaque patch qui change les récompenses, les limites ou les accès.

La compilation vérifie les contrats du SDK. Elle ne remplace pas une validation en jeu des états après connexion, changement de job, reset et changement de personnage. Les décalages mémoire relèvent de FFXIVClientStructs/Dalamud : attendre une version compatible après une mise à jour du client.

## Références de développement

- [Contrat IGameInventory](https://github.com/goatcorp/Dalamud/blob/master/Dalamud/Plugin/Services/IGameInventory.cs)
- [Réconciliation des changements d'inventaire](https://github.com/goatcorp/Dalamud/blob/master/Dalamud/Game/Inventory/GameInventory.cs)
- [Heavy Holoblade : objet officiel](https://na.finalfantasyxiv.com/lodestone/playguide/db/item/ea32db04a91/)
- [Ranperre Coin : objet officiel](https://na.finalfantasyxiv.com/lodestone/playguide/db/item/30fd80b9e04/)
- [Liste officielle des coffres de Windurst](https://na.finalfantasyxiv.com/lodestone/playguide/db/duty/e40698b1f19/)
- [Contrat IPlayerState](https://github.com/goatcorp/Dalamud/blob/master/Dalamud/Plugin/Services/IPlayerState.cs)
- [Contrat IUnlockState](https://github.com/goatcorp/Dalamud/blob/master/Dalamud/Plugin/Services/IUnlockState.cs)
- [InventoryManager : compteurs de monnaies](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/InventoryManager.cs)
- [UIState : niveau d'objet et accès](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/UI/UIState.cs)
- [InstanceContent : bonus des roulettes](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/UI/InstanceContent.cs)
- [PlayerState : verrouillage hebdomadaire et carnet](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/UI/PlayerState.cs)
- [AgentContentsFinderInterface : compteurs agrégés de récompenses](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/UI/Agent/AgentContentsFinderInterface.cs)
- [Loot : état des objets de la fenêtre de butin](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/UI/Loot.cs)
- [Achievement officiel : fin de la MSQ Windborne](https://na.finalfantasyxiv.com/lodestone/character/33969955/achievement/detail/3943/)
- [Quête officielle A Phantom Reborn](https://na.finalfantasyxiv.com/lodestone/playguide/db/quest/0964723485f/)

Les sources `main`/`master` peuvent évoluer. La compatibilité de cette version est contrôlée par compilation contre les assemblies Dalamud effectivement utilisées, puis doit être contrôlée en jeu.
