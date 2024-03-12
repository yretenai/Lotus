# Lotus
Evolution Engine (Warframe) tools

## Assets

The game splits up assets into data and header, they preserve the original filename so textures tend to have a ".PNG" suffix. 

- Models can sometimes be Lightwave LWO models but it's rare.
- Textures are almost always block compressed pixel data (see Oodle textures below), Textures are stored in OpenGL layout (smallest first) which is opposite of DirectX Layout (largest first)

## Oodle Textures

The Oodle Textures plugin library is statically linked, how the images are decoded is still up in the air but the texture data seems to be always block compressed streams in OpenGL layout.

## Cache Table

The TOC format is unchanged. Block frames have a new format. For both formats, there is no compression in place if both sizes are equal.

### The new format

Block frames in the cache files are now packed fields in a 64-bit big endian number when a certain bit is set.

- 5-bits is an unknown field(s) - seems to always be 0b00001, though. (Unknown1) - Suspected to be Compression type, but it's still 1 without compression.
- 29-bits is the size of the block before compression (BlockSize)
- 29-bits is the size of the block after compression (CompressedSize) - Oodle
- 1-bit is the "Use new format" flag - If 0, use the old format.

### The old format

The old format is two 16-bit signed big endian numbers.

- 16-bits for uncompressed size 
- 16-bits for compressed size - LZF

You can check if the uncompressed size is a negative number to see if it's using the new format. Alternatively check if the 8th bit is set if reading as unsigned numbers.

### Where is the header data?

The file caches are split up into 3 types, B, F and H - Base, Full and Header. The same filename will exist in each of the cache files. Certain serialized data exists in `/Packages.bin` in an INI-like format.
