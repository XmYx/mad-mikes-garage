# HD road cars: real-world references

Measurements only (published dimensions + profile/plan/face proportions derived from them and from period descriptions);
no images were downloaded or stored. All data lives in `refdata.py` (metres); profiles were refined from `tools/blender/cars.py`.

| Game car | Modelled after | L × W × H (mm) | Wheelbase (mm) | Source |
|---|---|---|---|---|
| Fiat126p | Fiat 126p | 3054 × 1378 × 1302 | 1840 | [Wikipedia: Fiat 126](https://en.wikipedia.org/wiki/Fiat_126) |
| Fiat500 | Fiat Nuova 500 (1957-75) | 2970 × 1320 × 1325 | 1840 | [autoevolution](https://www.autoevolution.com/cars/fiat-500-nouva-1957.html), [FastestLaps](https://fastestlaps.com/models/fiat-nuova-500) |
| FiatMultipla | Fiat Multipla (1998, pre-facelift) | 3994 × 1871 × 1670 | 2666 | [Wikipedia: Fiat Multipla](https://en.wikipedia.org/wiki/Fiat_Multipla) |
| LanciaYpsilon | Lancia Ypsilon 843 (2003-06) | 3778 × 1704 × 1530 | 2388 | [Wikipedia: Lancia Ypsilon](https://en.wikipedia.org/wiki/Lancia_Ypsilon) |
| Peugeot205 | Peugeot 205 5-door | 3705 × 1572 × 1370 | 2420 | [Wikipedia: Peugeot 205](https://en.wikipedia.org/wiki/Peugeot_205) |
| Peugeot206 | Peugeot 206 5-door | 3835 × 1652 × 1428 | 2442 | [Wikipedia: Peugeot 206](https://en.wikipedia.org/wiki/Peugeot_206) |
| Peugeot207CC | Peugeot 207 CC | 4037 × 1748 × 1397 (CC roof) | 2540 | [Wikipedia: Peugeot 207](https://en.wikipedia.org/wiki/Peugeot_207) |
| Peugeot405 | Peugeot 405 saloon | 4408 × 1716 × 1400 | 2669 | [Wikipedia: Peugeot 405](https://en.wikipedia.org/wiki/Peugeot_405) |
| Peugeot406Break | Peugeot 406 Break | 4736 × 1760 × 1450 (with rails) | 2700 | [Wikipedia: Peugeot 406](https://en.wikipedia.org/wiki/Peugeot_406) |
| Renault5 | Renault 5 (1972) 3-door | 3521 × 1525 × 1410 | 2419 | [Wikipedia: Renault 5](https://en.wikipedia.org/wiki/Renault_5) |
| CitroenBX | Citroën BX hatch | 4230 × 1660 × 1361 | 2655 | [Wikipedia: Citroën BX](https://en.wikipedia.org/wiki/Citro%C3%ABn_BX) |
| CitroenXM | Citroën XM berline | 4708 × 1793 × 1392 | 2850 | [Wikipedia: Citroën XM](https://en.wikipedia.org/wiki/Citro%C3%ABn_XM) |
| CitroenXantia | Citroën Xantia berline | 4440 × 1755 × 1380 | 2740 | [Wikipedia: Citroën Xantia](https://en.wikipedia.org/wiki/Citro%C3%ABn_Xantia) |
| Trabant | Trabant P601 limousine | 3555 × 1505 × 1440 | 2020 | [Wikipedia: Trabant 601](https://en.wikipedia.org/wiki/Trabant_601) |
| Interceptor | Ford Falcon XB GT hardtop + Pursuit Special nose | 4808 × 1900 × ~1330 | 2819 | [Wikipedia: Ford Falcon (XB)](https://en.wikipedia.org/wiki/Ford_Falcon_(XB)) (sedan figures; hardtop roof height estimated) |
| Pickup | Ford F-100 regular cab short bed (1973-79) | ~5030 × 2000 × 1800 | 2972 | [Wikipedia: F-Series 6th gen](https://en.wikipedia.org/wiki/Ford_F-Series_(sixth_generation)), [dimensions.com F-100 1976](https://www.dimensions.com/element/ford-f-100-1976-truck) |
| TowTruck | Ford F-350 one-ton wrecker (1973-79) | ~5650 × 2000 × 1850 | 3556 | [Wikipedia: F-Series 6th gen](https://en.wikipedia.org/wiki/Ford_F-Series_(sixth_generation)) (wheelbase); body length estimated |
| Wrecker | Ford F-600 class medium-duty conventional (1973-79) | ~7000 × 2300 × 2550 | 4800 (one of the offered wheelbases; matches the design) | estimated from class proportions |
| Coupe | Ford Capri Mk I (1969-74) | 4280 × 1646 × 1288 | 2560 | [Wikipedia: Ford Capri](https://en.wikipedia.org/wiki/Ford_Capri) |
| Sedan | Volvo 240 saloon (1981-93) | 4790 × 1710 × 1430 | 2649 | [Wikipedia: Volvo 200 series](https://en.wikipedia.org/wiki/Volvo_200_series) |
| Wagon | Jeep Wagoneer SJ (woody trim) | 4735 × 1900 × 1687 | 2794 | [Wikipedia: Jeep Wagoneer (SJ)](https://en.wikipedia.org/wiki/Jeep_Wagoneer_(SJ)) |
| Scavenger | Toyota Land Cruiser J60, armoured | 4750 × 1800 × 1815 | 2730 | [Toyota 75 years](https://www.toyota-global.com/company/history_of_toyota/75years/vehicle_lineage/car/id60013889/), [auto-data J60](https://www.auto-data.net/en/toyota-land-cruiser-j60-wagon-4.0-156hp-4wd-automatic-3748) |
| DuneBuggy | Meyers Manx | ~3450 × 1520 × ~1100 (cage extra) | ~2030 (VW pan shortened 14¼ in) | [Wikipedia: Meyers Manx](https://en.wikipedia.org/wiki/Meyers_Manx) |
| MonsterTruck | Chevrolet K10 square body short bed (1981-87) + ~1 m lift, 66 in tyres | 4860 × 2020 × 1770 stock | 2984 | [Wikipedia: Chevrolet C/K (3rd gen)](https://en.wikipedia.org/wiki/Chevrolet_C/K_(third_generation)) |

Not published in these sources and therefore estimated from class norms: track widths, tyre sizes (from the period
standard sizes, e.g. 126p 135/80 R12, BX 165/70 R14, F-100 G78-15), overhang split, and every profile point between the
published overall dimensions. Fiat 126p note: the real car has rectangular headlamps and tail lamps; it is modelled that way.
The fictional cars carry no real badges, names or logos.
