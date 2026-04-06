import re
import sys
import os

def resolve_includes(file_path, base_dir=None, processed_files=None):
    if processed_files is None:
        processed_files = set()

    if base_dir is None:
        base_dir = os.path.dirname(file_path)

    if not os.path.exists(file_path):
        return f"// ERROR: Could not find {file_path}\n"

    abs_path = os.path.abspath(file_path)
    if abs_path in processed_files:
        return "" # Prevent infinite recursion

    processed_files.add(abs_path)

    output = []
    include_regex = re.compile(r'^\s*#include\s+"([^"]+)"')

    with open(file_path, 'r') as f:
        for line in f:
            match = include_regex.match(line)
            if match:
                include_rel_path = match.group(1)
                include_full_path = os.path.normpath(os.path.join(os.path.dirname(abs_path), include_rel_path))
                output.append(resolve_includes(include_full_path, base_dir, processed_files))
            else:
                output.append(line)

    return "".join(output)

if __name__ == "__main__":
    if len(sys.argv) != 3:
        print("Usage: python preprocess_shaders.py <input_file> <output_file>")
        sys.exit(1)

    input_file = sys.argv[1]
    output_file = sys.argv[2]

    processed_content = resolve_includes(input_file)
    with open(output_file, 'w') as f:
        f.write(processed_content)
