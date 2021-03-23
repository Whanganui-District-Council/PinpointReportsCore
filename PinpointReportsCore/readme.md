# GDAL Compilation Notes for Ubuntu 20.04

```
cd /home/ubuntu/downloads/
mkdir gdal304
cd gdal304
```

```
curl -L http://download.osgeo.org/gdal/3.0.4/gdal-3.0.4.tar.gz | tar xz
```


```
cd gdal-3.0.4/
./configure --prefix `pwd`/build --without-python CFLAGS="-fPIC"
make
make install
```

```
export PATH=/home/ubuntu/downloads/gdal304/gdal-3.0.4/build/bin:$PATH
export LD_LIBRARY_PATH=/home/ubuntu/downloads/gdal304/gdal-3.0.4/build/lib:$LD_LIBRARY_PATH
export GDAL_DATA=/home/ubuntu/downloads/gdal304/gdal-3.0.4/build/share/gdal
gdalinfo version
```

```
cd swig/csharp
make veryclean
make interface
make
make test
```

```
cp .libs/libgdalcsharp.so.26.0.4 .libs/gdal_wrap.so
cp .libs/libgdalconstcsharp.so.26.0.4 .libs/gdalconst_wrap.so
cp .libs/libogrcsharp.so.26.0.4 .libs/ogr_wrap.so
cp .libs/libosrcsharp.so.26.0.4 .libs/osr_wrap.so
```


```
sudo cp .libs/*wrap.so /usr/lib/x86_64-linux-gnu/
```

##Runtime dll's for Pinpoint Reports

 
```
cp /home/ubuntu/downloads/gdal304/gdal-3.0.4/swig/csharp/gdal_csharp.dll /opt/pinpointreports/
cp /home/ubuntu/downloads/gdal304/gdal-3.0.4/swig/csharp/gdalconst_csharp.dll /opt/pinpointreports/
cp /home/ubuntu/downloads/gdal304/gdal-3.0.4/swig/csharp/ogr_csharp.dll /opt/pinpointreports/
cp /home/ubuntu/downloads/gdal304/gdal-3.0.4/swig/csharp/osr_csharp.dll /opt/pinpointreports/
```
