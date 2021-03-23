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

# Connection Strings
```
sudo nano /home/ubuntu/pinpointreports/appsettings.json
```
```
  "ConnectionStrings": {
    "pgwdcgisodbc": "Driver={PostgreSQL Unicode};Server=servername;Port=5432;Database=database;Uid=username;Pwd=password;",
    "pgwdcgisogr": "host=hostname port=5432 dbname=database user=username password='password'"
  },
```
