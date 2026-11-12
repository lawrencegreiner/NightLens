HEALPix validation fixture

Healpix-reference.csv contains nested pixel indices for the listed ICRS
coordinates and orders. Generated with astropy-healpix 1.1.2's compiled
lonlat_to_healpix_nested reference routine, using NumPy radians. The app uses
an independent C# implementation of the HEALPix/HiPS mapping, checked against
these fixed values. No Python package is required to build or run NightLens.

Reference implementation: https://github.com/astropy/astropy-healpix
HiPS standard: https://www.ivoa.net/documents/HiPS/20170519/REC-HIPS-1.0-20170519.pdf
The HEALPix grid and the HiPS image grid have different orientation. A 90-degree
rotation into the FITS tile grid followed by the JPEG vertical flip means the
JPEG column is nested y and JPEG row is nested x. The mapping was checked
against hipspy's healpix_to_hips_tile / HipsTile.from_numpy transformation:
https://github.com/hipspy/hips/blob/master/hips/draw/healpix.py
https://github.com/hipspy/hips/blob/master/hips/tiles/tile.py
This geometric convention is implemented independently in C#; no Python code
or Python runtime is distributed with the application.