import os
import re

def main():
    shader_dir = "/tmp/planetary_terrain_renderer_rust/src/shaders/tiling_prepass/"
    out_dir = "Shaders/TilingPrepass/"

    if not os.path.exists(out_dir):
        os.makedirs(out_dir)

    for f in os.listdir(shader_dir):
        if f.endswith('.wgsl'):
            print(f"Translating {f}...")
            with open(os.path.join(shader_dir, f), 'r') as infile:
                content = infile.read()

            content = re.sub(r'fn (\w+)', r'void \1', content)
            content = re.sub(r'var<private> (\w+) : (\w+);', r'\2 \1;', content)
            content = re.sub(r'var<workgroup> (\w+) : (\w+);', r'groupshared \2 \1;', content)
            content = re.sub(r'let (\w+) : (\w+) =', r'const \2 \1 =', content)
            content = re.sub(r'let (\w+) =', r'const auto \1 =', content)
            content = re.sub(r'var (\w+) : (\w+) =', r'\2 \1 =', content)
            content = re.sub(r'var (\w+) =', r'auto \1 =', content)
            content = re.sub(r'f32', r'float', content)
            content = re.sub(r'u32', r'uint', content)
            content = re.sub(r'i32', r'int', content)
            content = re.sub(r'vec2<f32>', r'float2', content)
            content = re.sub(r'vec3<f32>', r'float3', content)
            content = re.sub(r'vec4<f32>', r'float4', content)
            content = re.sub(r'vec2<u32>', r'uint2', content)
            content = re.sub(r'vec3<u32>', r'uint3', content)
            content = re.sub(r'vec4<u32>', r'uint4', content)
            content = re.sub(r'vec2<i32>', r'int2', content)
            content = re.sub(r'vec3<i32>', r'int3', content)
            content = re.sub(r'vec4<i32>', r'int4', content)
            content = re.sub(r'mat4x4<f32>', r'float4x4', content)
            content = re.sub(r'mat3x3<f32>', r'float3x3', content)
            content = re.sub(r'array<(\w+), (\d+)>', r'\1[\2]', content)
            content = re.sub(r'struct (\w+) {([^}]*)}', r'struct \1 {\2};', content)

            out_content = "/* Translated from WGSL */\n" + content
            out_file = f.replace('.wgsl', '.hlsl')

            with open(os.path.join(out_dir, out_file), 'w') as outfile:
                outfile.write(out_content)

if __name__ == "__main__":
    main()
