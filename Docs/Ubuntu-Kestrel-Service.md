[< Home](readme.md)

# Setup Service for Kestrel

## Linux Service
* https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/linux-apache?view=aspnetcore-3.1



* Apache is setup to forward requests made to http://localhost:80 to the ASP.NET Core app running on Kestrel at http://127.0.0.1:5000. However, Apache isn't set up to manage the Kestrel process. Use systemd and create a service file to start and monitor the underlying web app. systemd is an init system that provides many powerful features for starting, stopping, and managing processes.


## Create the service file
* Create the service definition file:

```
sudo nano /etc/systemd/system/kestrel-pinpointreports.service
```

* An example service file for the app:

```
[Unit]
Description=pinpointreports .NET Web API App running on Ubuntu

[Service]
WorkingDirectory=/opt/pinpointreports
ExecStart=/usr/share/dotnet/dotnet /opt/pinpointreports/PinpointReportsCore.dll
Restart=always
# Restart service after 10 seconds if the dotnet service crashes:
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=dotnet-pinpointreports
User=ubuntu
Environment=ASPNETCORE_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target

```

## stop/start service...

```
cd /opt/pinpointreports

sudo service kestrel-pinpointreports stop

sudo service kestrel-pinpointreports start
```

# Running manually (when service stopped or for debugging in console)...

```
cd /opt/pinpointreports

dotnet PinpointReportsCore.dll
```

# Configure apache to reverse proxy Pinpoint reports

```
sudo nano /etc/apache2/sites-available/mysitename.conf
```
* add...
```
<VirtualHost *:*>
    RequestHeader set "X-Forwarded-Proto" expr=%{REQUEST_SCHEME}
</VirtualHost>
```
* then add to existing reverse proxies...
```
    ProxyPass /pinpointreports/ http://127.0.0.1:5000/pinpointreports/report/
    ProxyPassReverse /pinpointreports/ http://127.0.0.1:5000/pinpointreports/report/
```

* Enable headers module
```
sudo a2enmod headers
```

* Restart Apache
```
sudo service apache2 restart
```
