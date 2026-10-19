"""Blender 2.79 script (run by tools/build_wolf.py): renders Danimal's Wolf.blend in Flare's 8 directions, as its own
Render8Dirs text block does, at a chosen resolution percentage (the scene is 512 px; 25 gave the old 128-px sheet).

blender -b Wolf.blend -P wolf_render_blender.py -- <out dir> <percent> <first frame> <last frame>
"""
import bpy, sys, os
from math import radians
argv = sys.argv[sys.argv.index("--") + 1:]
out, percent, first, last = argv[0], int(argv[1]), int(argv[2]), int(argv[3])
sc = bpy.data.scenes[0]
sc.render.resolution_percentage = percent
sc.render.alpha_mode = 'TRANSPARENT'
sc.render.image_settings.file_format = 'PNG'
sc.render.image_settings.color_mode = 'RGBA'
sc.frame_start, sc.frame_end = first, last
platform = bpy.data.objects["RenderPlatform"]
for i in range(8):
    rot = platform.rotation_euler
    rot[2] = rot[2] - radians(-45)
    platform.rotation_euler = rot
    sc.render.filepath = os.path.join(out, "d{}_".format(i))
    bpy.ops.render.render(animation=True)
