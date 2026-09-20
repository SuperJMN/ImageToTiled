# ImageToTiled

Herramienta y biblioteca en C# para migrar imágenes a mapas de [Tiled](https://www.mapeditor.org/) (`.tmx` + `.tsx` + textura PNG de tileset).

Diseñada específicamente para flujos de trabajo de desarrollo retro con [RetroSharp](https://github.com/SuperJMN/RetroSharp) (Game Boy, NES, SNES) y motores de videojuegos en general.

---

## Características

- **Deduplicación exacta de tiles**: Divide la imagen en teselas (por defecto 16×16 px) e identifica tiles idénticos mediante hashing FNV-1a y comparación de bytes.
- **Empaquetado de Tileset**: Genera una hoja de tiles compacta en PNG (por defecto a 16 columnas / 256 px de ancho, o configurable a disposición cuadrada).
- **Formatos Tiled estándar**:
  - Archivo `.tsx` con especificación XML Tiled 1.10.
  - Archivo `.tmx` con codificación CSV de celdas y referencias relativas portables.
- **Manejo de Transparencias (`EmptyMode`)**:
  - `Tile` (por defecto): El tile 0 del tileset se reserva para vacío transparente (GID 1 en el mapa). Todas las celdas del mapa contienen un GID válido $\ge 1$, garantizando compatibilidad con compiladores retro como RetroSharp.
  - `Gid0`: Las celdas transparentes se marcan con GID 0 en el mapa y no se incluyen en el tileset (comportamiento nativo de celda vacía en Tiled).
  - `None`: Las teselas transparentes se tratan como cualquier otro tile gráfico.
- **Color Keying**: Soporte para definir un color cromático (ej. `#FF00FF`) que se convertirá en transparente.
- **Propiedades RetroSharp**: Inclusión automática de propiedades personalizadas requeridas por el importador de mundos de RetroSharp (`retrosharpStreamY`, `retrosharpWorldY`, `retrosharpWorldHeight`).
- **Verificación sin pérdidas**: Reconstruye la imagen completa a partir de los tiles generados y la cuadrícula del mapa, asegurando una coincidencia 100% idéntica píxel a píxel.

---

## Estructura de la solución

```
ImageToTiled/
├── src/
│   ├── ImageToTiled/         # Biblioteca principal (Core)
│   └── ImageToTiled.Cli/     # Aplicación de línea de comandos (CLI)
├── test/
│   └── ImageToTiled.Tests/   # Pruebas unitarias e integración (xUnit + FluentAssertions)
├── ImageToTiled.sln          # Solución clásica
└── ImageToTiled.slnx         # Solución XML moderna (.NET)
```

---

## Uso de la CLI

### Sintaxis básica

```bash
dotnet run --project src/ImageToTiled.Cli -- <ruta_imagen> [opciones]
```

O compilando el binario ejecutable:

```bash
dotnet build -c Release
./src/ImageToTiled.Cli/bin/Release/net8.0/ImageToTiled.Cli <ruta_imagen>
```

### Opciones de la línea de comandos

| Opción | Descripción | Valor por defecto |
| :--- | :--- | :--- |
| `image` *(posicional)* | Ruta de la imagen de entrada (PNG, etc.). | *Requerido* |
| `-o, --output-dir` | Carpeta de salida para los archivos generados. | Misma carpeta que la imagen |
| `-n, --name` | Nombre base para el mapa, tileset y ficheros. | Nombre de la imagen sin extensión |
| `--tile-size` | Tamaño de tile cuadrado (establece ancho y alto). | - |
| `--tile-width` | Ancho de cada tesela en píxeles. | `16` |
| `--tile-height` | Alto de cada tesela en píxeles. | `16` |
| `--columns` | Columnas en la hoja de tileset, o `square` / `auto`. | `16` |
| `--empty-mode` | Manejo de transparencias: `tile`, `gid0`, `none`. | `tile` |
| `--color-key` | Color hexadecimal a convertir en transparente (ej. `#FF00FF`). | - |
| `--layer-name` | Nombre de la capa de tiles en el TMX. | `world` |
| `--tileset-image` | Nombre personalizado para el archivo PNG del tileset. | `{name}_tiles.png` |
| `--tmx` | Nombre personalizado para el archivo `.tmx`. | `{name}.tmx` |
| `--tsx` | Nombre personalizado para el archivo `.tsx`. | `{name}.tsx` |
| `--retrosharp` | Incluir propiedades personalizadas de RetroSharp. | `true` |
| `--no-retrosharp` | Omitir propiedades de RetroSharp en el TMX. | - |
| `--stream-y` | Valor de `retrosharpStreamY`. | `0` |
| `--world-y` | Valor de `retrosharpWorldY`. | `0` |
| `--world-height` | Valor de `retrosharpWorldHeight`. | Alto del mapa en tiles |
| `-f, --force` | Forzar sobreescritura si los archivos existen. | `false` |
| `--no-verify` | Omitir la verificación de reconstrucción píxel a píxel. | `false` |

### Ejemplos

#### 1. Migración rápida
Genera `Part2.tmx`, `Part2.tsx` y `Part2_tiles.png` en el mismo directorio:

```bash
dotnet run --project src/ImageToTiled.Cli -- /home/jmn/Escritorio/SMB2/Part2.png
```

#### 2. Exportación a carpeta con empaquetado cuadrado
```bash
dotnet run --project src/ImageToTiled.Cli -- /home/jmn/Escritorio/SMB2/Part2.png \
  -o ./assets/maps \
  --name stage1 \
  --columns square \
  --empty-mode tile
```

#### 3. Uso con Color Key (ej. Magenta) y modo Gid0
```bash
dotnet run --project src/ImageToTiled.Cli -- sprite_sheet.png \
  --color-key "#FF00FF" \
  --empty-mode gid0
```

---

## Uso como biblioteca C#

Agrega la referencia a `ImageToTiled.csproj`:

```csharp
using ImageToTiled;

var options = new ConversionOptions
{
    TileWidth = 16,
    TileHeight = 16,
    Columns = 16,
    EmptyMode = EmptyMode.Tile,
    RetrosharpProperties = true
};

ConversionResult result = ImageToTiledConverter.Convert("Part2.png", "output/maps", options);

Console.WriteLine($"Mapa generado: {result.TmxPath}");
Console.WriteLine($"Tiles únicos: {result.UniqueTilesCount}");
Console.WriteLine($"Reconstrucción lossless: {result.VerifiedLossless}");
```

---

## Ejecución de pruebas

```bash
dotnet test
```

## Licencia

[MIT License](LICENSE) © José Manuel Nieto (SuperJMN)
