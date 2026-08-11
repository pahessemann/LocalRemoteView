# LocalRemoteView

LocalRemoteView affiche et contrôle un PC Windows depuis un second PC du même réseau local. L’agent hôte ne place **aucun bandeau, contour ou marqueur sur le bureau**. Son état reste accessible dans la zone de notification Windows.

## Les deux fichiers à utiliser

- `Livraison\LocalRemoteView-Installer.exe` : à lancer une seule fois sur le PC à contrôler ;
- `Livraison\LocalRemoteView.exe` : à lancer sur le PC depuis lequel vous contrôlez.

Ils sont autonomes : aucun runtime, aucune DLL et aucun autre fichier ne sont nécessaires. Windows 10 ou 11 x64 et un réseau local configuré comme privé suffisent.

## Installation rapide

1. Copier `LocalRemoteView-Installer.exe` sur le PC à contrôler et l’exécuter. Accepter la demande administrateur Windows.
2. La configuration initiale s’ouvre une seule fois. Conserver le port proposé, copier la clé et enregistrer.
3. Copier `LocalRemoteView.exe` sur le PC contrôlant et l’exécuter.
4. Saisir l’IPv4 du PC hôte, le port et la clé, puis cliquer **Connexion**.

Les sessions suivantes démarrent automatiquement à l’ouverture de session du PC hôte. Le clic droit sur l’icône bouclier de la zone de notification permet de voir l’état, recopier la clé ou arrêter l’agent.

## Commandes

- Cliquer dans l’image pour donner le focus au contrôle distant.
- `F11` active ou quitte le plein écran.
- Les clics, la molette, les déplacements de souris et les touches sont transmis lorsque l’image a le focus.

## Configuration de l’hôte

Le fichier `%LOCALAPPDATA%\LocalRemoteView\host.json` contient :

- `Port` : port TCP (45821 par défaut) ;
- `FramesPerSecond` : 5 à 60 ;
- `MaxWidth` : largeur maximale transmise ;
- `JpegQuality` : qualité JPEG de 25 à 95 ;
- `AllowedClientIp` : IPv4 cliente unique autorisée, ou chaîne vide pour tout le LAN privé.

Redémarrer l’agent après modification. Le pare-feu créé par l’installateur limite l’accès au sous-réseau local et au profil privé. Le protocole exige une clé aléatoire de 256 bits, authentifie les deux côtés puis chiffre chaque message en AES-256-GCM.

## Choix d’architecture Windows

La capture et l’injection d’entrées doivent fonctionner dans la session interactive de Windows. Un service classique s’exécute en session 0 et ne peut pas réaliser correctement ces opérations. L’installateur utilise donc une tâche Windows à l’ouverture de session qui lance un agent `WinExe` sans fenêtre. C’est le modèle adapté à Windows 10/11 pour ce type d’application.

## Limites connues

- L’écran sécurisé Windows (UAC, ouverture de session, `Ctrl+Alt+Suppr`) n’est ni capturé ni contrôlable avec `SendInput`.
- Le JPEG privilégie la simplicité et une faible dépendance. Pour une charge réseau moindre en 4K/60, l’évolution recommandée est DXGI + Media Foundation H.264.
- Une seule session cliente simultanée est acceptée.

## Désinstallation

Exécuter en administrateur `scripts\Uninstall-Host.ps1`. Ajouter `-RemoveConfiguration` pour supprimer également la clé, les paramètres et le journal locaux.

## Compilation et test

```powershell
.\.dotnet\dotnet.exe build .\LocalRemoteView.sln -c Release
.\.dotnet\dotnet.exe run --project .\tests\LocalRemoteView.SelfTest -c Release
```
