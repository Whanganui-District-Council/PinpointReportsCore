[< Home](readme.md)

# GDAL 3.0.4 Install


```
sudo apt-get update

sudo apt update.
sudo apt install libpq-dev
sudo apt install gdal-bin
sudo apt install libgdal-dev
```

* test GDAL

```
gdalinfo --version
```





# gdal files for dotnet runtime
* copy **.so** to **/usr/lib/x86_64-linux-gnu**

```
/usr/lib/x86_64-linux-gnu/gdal_wrap.so
/usr/lib/x86_64-linux-gnu/gdalconst_wrap.so
/usr/lib/x86_64-linux-gnu/ogr_wrap.so
/usr/lib/x86_64-linux-gnu/osr_wrap.so

```
