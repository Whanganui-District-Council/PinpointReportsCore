# ODBC Setup on Ubuntu for PostgreSQL


```
sudo apt-get update
```



***

* Install the basic config tools for the UNIX ODBC if required:
Check first
```
odbcinst -j
```
```
sudo apt-get install unixodbc-bin
sudo apt-get install unixodbc
```

***

* ODBC drivers for PostgreSQL:
```
sudo apt-get install odbc-postgresql
```

* Apply the template file provided to setup driver entries:
```
sudo odbcinst -i -d -f /usr/share/psqlodbc/odbcinst.ini.template
```

* Setup the a sample DSN
```
sudo odbcinst -i -s -l  -n adyoung-pg -f /usr/share/doc/odbc-postgresql/examples/odbc.ini.template
```

* Now modify `sudo nano /etc/odbc.ini` according to your DB:

```
[wdcgis]
Description=PostgreSQL
Driver=PostgreSQL ANSI
Trace=No
TraceFile=/tmp/psqlodbc.log
Database=database
Servername=servername
UserName=username
Password=password
Port=5432
ReadOnly=Yes
RowVersioning=No
ShowSystemTables=No
ShowOidColumn=No
FakeOidIndex=No
ConnSettings=
```


* Now modify `sudo nano /etc/odbcinst.ini`:

```
[PostgreSQL ANSI]
Description=PostgreSQL ODBC driver (ANSI version)
Driver=/usr/lib/x86_64-linux-gnu/odbc/psqlodbca.so
Setup=/usr/lib/x86_64-linux-gnu/odbc/libodbcpsqlS.so
Debug=0
CommLog=1
UsageCount=1

[PostgreSQL Unicode]
Description=PostgreSQL ODBC driver (Unicode version)
Driver=/usr/lib/x86_64-linux-gnu/odbc/psqlodbcw.so
Setup=/usr/lib/x86_64-linux-gnu/odbc/libodbcpsqlS.so
Debug=0
CommLog=1
UsageCount=1
```

* Check Connection
```
isql -v username
```

# ODBC Setup on Ubuntu for MS SQL Server


```
sudo apt-get update
```

***

```
sudo su
curl https://packages.microsoft.com/keys/microsoft.asc | apt-key add -
curl https://packages.microsoft.com/config/ubuntu/20.04/prod.list > /etc/apt/sources.list.d/mssql-release.list
exit

sudo apt-get update
sudo ACCEPT_EULA=Y apt-get install msodbcsql17

```

### Optional
```

# optional: for bcp and sqlcmd
sudo ACCEPT_EULA=Y apt-get install mssql-tools
echo 'export PATH="$PATH:/opt/mssql-tools/bin"' >> ~/.bash_profile
echo 'export PATH="$PATH:/opt/mssql-tools/bin"' >> ~/.bashrc
source ~/.bashrc
# optional: for unixODBC development headers
sudo apt-get install unixodbc-dev

```





# Connection Strings
```
sudo nano /home/ubuntu/pinpointreports/appsettings.json
```
```
  "ConnectionStrings": {
    "pgwdcgisodbc": "Driver={PostgreSQL Unicode};Server=servername;Port=5432;Database=database;Uid=username;Pwd=password;",
    "pgwdcgisogr": "host=hostname port=5432 dbname=database user=username password='password'",
    "mssqlodbc": "Driver={ODBC Driver 17 for SQL Server};Server=servername;Port=1433;Database=database;Uid=username;Pwd=password;"
  },
```
