# Sources du catalogue et limites de connaissance

Vérification effectuée le **20 septembre 2026**, sur les sources primaires ci-dessous. Le catalogue vise le client international de FFXIV, **niveau 100, patch 7.56**. Le [journal officiel des patchs](https://na.finalfantasyxiv.com/lodestone/special/patchnote_log/) confirme cette version. Les régions dont le calendrier de publication diffère nécessitent un catalogue distinct.

Ce document décrit les données de référence et les règles attendues. Il ne constitue pas une preuve de validation du plugin dans un client FFXIV en cours d'exécution. Les méthodes exposées par les bibliothèques doivent aussi être vérifiées contre les assemblies utilisées lors de la compilation.

## Paliers de contenu

Les iLvl d'entrée sont des conditions d'accès à la recherche de mission. Ils ne mesurent ni la maîtrise d'un combat, ni la qualité des statistiques secondaires. Certains contenus permettent des exceptions d'inscription en groupe complet ; le plugin doit présenter le seuil comme une indication d'accès standard.

| Contenu ou équipement | Niveau d'objet | Rôle dans la progression | Source |
| --- | --- | --- | --- |
| Historia / équipement du roman royal | Équipement 750 | Rattrapage avec Mathematics chez Zircon ; comparer les coûts | [Objet officiel](https://na.finalfantasyxiv.com/lodestone/playguide/db/item/bbe5d772aba/) et [échanges 7.2](https://na.finalfantasyxiv.com/lodestone/topics/detail/3c4910f373e497acd3428c37f6358e341e4cc06d/) |
| Mistwake | Entrée 735 ; équipement 755 | Rattrapage et progression de l'épopée | [7.4](#source-74) |
| AAC Heavyweight normal | Entrée 745 ; équipement 770 | Rattrapage par jetons | [7.4](#source-74) |
| Courtly Lover | Équipement 770 | Fabrication ou achat selon le budget du joueur | [7.4](#source-74) |
| Hell on Rails Extreme | Entrée 760 | Combat optionnel difficile | [7.4](#source-74) |
| AAC Heavyweight Savage M1 / M2 / M3 / M4 | Entrées 760 / 765 / 770 / 770 | Progression Savage choisie par le joueur | [7.4](#source-74) |
| Roulette Expert | Entrée 750 ; Mistwake et The Clyteum | Monnaies et bonus quotidien | [7.5](#source-75) |
| The Clyteum | Entrée 750 ; équipement Praemagitek 765 | Rattrapage et épopée | [7.5](#source-75) |
| Windurst: The Third Walk | Entrée 755 ; équipement 780 | Alliance, pièce et monnaie d'amélioration | [7.5](#source-75) |
| The Unmaking Extreme | Entrée 770 ; armes 785 | Arme et progression Extreme | [7.5](#source-75) |
| Courtly Lover augmenté | Équipement 780 | Amélioration du craft | [7.5](#source-75) |
| Augmented Bygone Brass | Équipement 790 | Amélioration des mémoquartz | [Base d'objets officielle](https://na.finalfantasyxiv.com/lodestone/playguide/db/item/71d8cf43d18/) |
| Grand Champion | Armure 790 | Récompense de Savage | [Base d'objets officielle](https://na.finalfantasyxiv.com/lodestone/playguide/db/item/0396f8feab4/) |
| Armes Grand Champion | Armes 795 | Palier d'arme Savage, distinct de l'armure | [Base d'objets officielle](https://na.finalfantasyxiv.com/lodestone/playguide/db/item/21e3f444139/) |
| Phantom Eclipticum / Occultum | Armes 790 / 795 | Progression d'arme Phantom | [7.55](#source-755) |

Une entrée de catalogue doit distinguer le niveau requis, l'iLvl d'entrée et celui de la récompense. Une augmentation de l'iLvl moyen n'est pas nécessairement un gain sur chaque emplacement. Le moteur doit favoriser une amélioration pertinente du personnage et de son job actif, sans promettre un équipement optimal (« BiS ») sur la seule base de l'iLvl.

Pour le craft de rattrapage, `Courtly Lover` correspond en français à l'équipement « de courtisan assidu ». La [fiche officielle du surcot](https://na.finalfantasyxiv.com/lodestone/playguide/db/item/a6886528380/) confirme i770 et sa recette. Comparer les versions de haute qualité et les statistiques ; ne pas déduire le prix de marché ou l'accessibilité financière à partir du niveau du personnage.

## Récompenses récurrentes au patch 7.56

| Suivi | Règle à appliquer | Source |
| --- | --- | --- |
| Mémoquartz Mnemonics | 900 obtenus par semaine ; stockage maximal 2 000 | [7.56](#source-756) |
| Mémoquartz Mathematics | Aucun plafond d'obtention hebdomadaire ; stockage maximal 2 000 | [7.4](#source-74) |
| Jetons d'équipement Heavyweight normal | Farmables sans restriction hebdomadaire | [7.5](#source-75) |
| Heavy Holoblade de Heavyweight M4 normal | Reste hebdomadaire ; 4 lames contre un Universal Tomestone | [7.5](#source-75) |
| Windurst : équipement | Une pièce hebdomadaire, distincte de la récompense de complétion | [7.5](#source-75) |
| Windurst : récompense de complétion | Ranperre Coin hebdomadaire | [7.5](#source-75) |
| Heavyweight Savage | Restrictions hebdomadaires supprimées ; coffres toujours disponibles | [7.56](#source-756) |
| Dancing Mad Ultimate | Récompense hebdomadaire ; activité réservée à une préférence explicite | [7.51](#source-751) |

Le reset hebdomadaire mentionné dans les notes officielles est **mardi à 08:00 UTC**. Les conversions en heure française varient avec l'heure d'été. Les cycles quotidiens doivent être traités séparément ; ne pas leur appliquer l'heure du reset hebdomadaire.

Les restrictions Heavyweight Savage du lancement 7.4 sont obsolètes pour ce catalogue. Ne pas reprendre un ancien guide sans appliquer les modifications suivantes. La possession d'un jeton ne permet pas de déterminer sa date d'acquisition ; sa dépense ne rétablit pas le droit à une récompense.

Pour le carnet de Khloe, la disponibilité hebdomadaire et la date limite du carnet sont distinctes : les [notes 3.4](https://na.finalfantasyxiv.com/lodestone/topics/detail/159309cb1ef0706cf37a9a91e3e18865b70097a6) décrivent un carnet disponible chaque mardi et valable deux semaines à partir de son mardi d'émission. Les [notes 6.5](https://na.finalfantasyxiv.com/lodestone/topics/detail/1dcbf39c97285ba9a42012eecf2c031f0ffbceb1) imposent de réclamer les récompenses du carnet précédent avant d'en recevoir un nouveau. Un simple booléen de complétion hebdomadaire ne représente pas tous ces états.

## Déblocages et progression permanente

Le niveau 100 ne prouve pas que l'épopée ou les chaînes de déblocage sont terminées. Préférer l'état du contenu lu dans le client à une déduction depuis le niveau, le stuff ou le nom d'un équipement. Les identifiants de quêtes doivent être extraits et vérifiés dans les feuilles du jeu ; aucun identifiant ne doit être inventé depuis son nom.

Repères pour une résolution des noms anglais dans les feuilles locales : [Dawntrail](https://na.finalfantasyxiv.com/lodestone/playguide/db/quest/7a0da925036/) pour la fin de l'épopée initiale ; [Trail to the Heavens](https://na.finalfantasyxiv.com/lodestone/playguide/db/quest/da0bb834108/) pour le jalon 7.5 ; `Windborne` pour le jalon associé au succès final `Onward and Upward`, dont le [texte officiel](https://na.finalfantasyxiv.com/lodestone/character/33969955/achievement/detail/3943/) confirme ce nom. Les notes 7.56 nomment la première quête `A Winter's Dream` mais masquent les suivantes ; corroborer la résolution dans les données du client. Les identifiants hexadécimaux des URL Lodestone ne sont pas des identifiants de quêtes utilisables dans l'API du jeu.

Pour les armes Phantom, la mise à jour 7.55 ajoute les deux derniers stades. « Under No Illusion » suit « A Phantom Reborn » ; les armes suivantes utilisent notamment les Mathematics. Une arme cible et les étapes déjà accomplies doivent être prises en compte avant de recommander ce parcours. [Source 7.55](#source-755)

Lorsque les prérequis forment une chaîne, proposer le premier blocage utile. Si un contenu n'est pas débloqué, afficher une étape de déblocage avant de proposer son farm. Masquer les noms de contenu futurs si une option de protection contre les spoilers est active.

## Ce que le client permet de lire

Les références suivantes sont les sources du projet FFXIVClientStructs, consultées sur sa branche principale. Elles décrivent des capacités disponibles, pas la garantie que toutes sont implémentées dans cette version du plugin.

| Donnée du personnage connecté | Point d'accès de référence |
| --- | --- |
| Équipement porté et stock d'objets | `InventoryManager.GetInventoryContainer`, `GetInventoryItemCount` |
| Monnaie détenue | `InventoryManager.GetTomestoneCount(itemId)` |
| Mémoquartz plafonnés acquis ce cycle | `InventoryManager.GetWeeklyAcquiredTomestoneCount()` |
| Plafond courant annoncé par le client | `InventoryManager.GetLimitedTomestoneWeeklyLimit()` |
| iLvl affiché dans la fenêtre personnage | `UIState.CurrentItemLevel` |
| Contenu débloqué / déjà terminé | `UIState.IsInstanceContentUnlocked`, `IsInstanceContentCompleted` |
| Quête achevée | `QuestManager.IsQuestComplete(ushort)` |
| Bonus roulette déjà obtenu | `InstanceContent.IsRouletteComplete(byte)` |

Sources : [InventoryManager.cs](https://raw.githubusercontent.com/aers/FFXIVClientStructs/main/FFXIVClientStructs/FFXIV/Client/Game/InventoryManager.cs), [UIState.cs](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/UI/UIState.cs), [QuestManager.cs](https://raw.githubusercontent.com/aers/FFXIVClientStructs/main/FFXIVClientStructs/FFXIV/Client/Game/QuestManager.cs), [InstanceContent.cs](https://raw.githubusercontent.com/aers/FFXIVClientStructs/main/FFXIVClientStructs/FFXIV/Client/Game/UI/InstanceContent.cs).

**Attention aux espaces d'identifiants :** `InstanceContent` et `ContentFinderCondition` ne sont pas interchangeables. La liaison doit suivre les relations des feuilles du jeu. Une méthode `Is...Completed` désigne une complétion permanente et ne constitue pas à elle seule un indicateur hebdomadaire.

## Inconnus et déclarations manuelles

- L'état complet est celui du personnage connecté. Niveau/job visibles ou équipement inspecté d'un autre joueur ne révèlent pas ses quêtes, monnaies ou récompenses hebdomadaires.
- Un combat terminé n'implique pas qu'une pièce d'équipement a été gagnée. Windurst doit conserver deux pistes de récompense distinctes.
- Un inventaire vide ne prouve pas qu'une récompense n'a pas déjà été obtenue puis dépensée.
- Une nouvelle installation ne connaît pas toutes les activités effectuées avant son lancement. L'absence d'observation signifie « inconnu ».
- Temps disponible, goût pour le Savage, objectif personnel, progression mécanique et budget d'achat relèvent d'une préférence ou déclaration du joueur.
- Une donnée non chargée, indisponible ou lue sur une version incompatible doit rester inconnue. Une valeur zéro n'est pas un substitut universel à l'absence de donnée.

Chaque état persistant doit porter son personnage, sa provenance (`Live`, `Observed`, `Manual`, `Unknown`) et son cycle ou sa date de lecture. Un état temporaire d'un ancien cycle ne doit pas masquer une activité après reset. Les validations de quêtes permanentes ne doivent pas être effacées par le reset.

## Entretien du catalogue

Conserver pour chaque règle son patch, sa date de vérification et ses sources. Les noms, plafonds, iLvl et restrictions peuvent évoluer indépendamment du code du plugin. Lors d'un écart de version, afficher la version du catalogue et ne pas présenter sa validité future comme acquise.

Ordre recommandé pour une révision : journal officiel, notes du nouveau patch, éventuelles corrections ultérieures, feuilles de données du client, compilation avec le SDK cible, puis validation en jeu. Les préférences de score du moteur sont des choix du produit, pas des faits publiés par Square Enix.

## Sources de contenu

### Source 7.4

[Patch 7.4 Notes — Lodestone](https://na.finalfantasyxiv.com/lodestone/topics/detail/06944d892fd98cc00b2a28ff77edbafa4f7eef54/). Sert aux seuils initiaux et aux familles d'équipement ; ses anciens plafonds sont remplacés par les patchs suivants.

### Source 7.5

[Patch 7.5 Notes — Lodestone](https://na.finalfantasyxiv.com/lodestone/topics/detail/07320affa7e0fcd9685afcbe54fbf55405b6d822/). Sert aux nouveaux contenus, à l'Expert et aux règles Heavyweight normal / Windurst.

### Source 7.51

[Patch 7.51 Notes — Lodestone](https://na.finalfantasyxiv.com/lodestone/topics/detail/c46881a31a2c90d0965493c921b434eca09113f8/). Sert à Dancing Mad Ultimate et confirme le reset en UTC.

### Source 7.55

[Patch 7.55 Notes — Lodestone](https://na.finalfantasyxiv.com/lodestone/topics/detail/99b6bfb8ecac428c7d3bb37dcb84b52f1064320b/). Sert aux armes Phantom et à leur progression finale.

### Source 7.56

[Patch 7.56 Notes — Lodestone](https://na.finalfantasyxiv.com/lodestone/topics/detail/a8a526ad64db45c8ca8d1c7fdcce8a5eedaa18bc/). Sert au plafond de 900, à la suppression des restrictions Savage et à l'état de version actuel.
