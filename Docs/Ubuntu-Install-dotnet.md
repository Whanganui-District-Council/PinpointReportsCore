[< Home](readme.md)

# Install dotnet

## Install SDK

```
sudo apt-get update
sudo apt-get install -y apt-transport-https

sudo apt-get update
sudo apt-get install -y dotnet-sdk-3.1
```

## Install ASP.Net Core Runtime

```
sudo apt-get install -y aspnetcore-runtime-3.1
```

## environment path
* set environment path
```
sudo nano /etc/profile.d/pinpointreports_path.sh
```
* add...
```
export PATH="/opt/pinpointreports/:$PATH"
```

* Close and restart shell
* Check...
```
env
```

## Additional Libraries
```
sudo apt-get install libgdiplus
sudo apt-get install libc6-dev

```
