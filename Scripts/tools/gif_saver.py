import os
import sys
import json
from PIL import Image

def save_images_as_gif(folder_path, output_gif_name, delay):
    images = []
    try:
        frame_files = sorted(os.listdir(folder_path))
        saved, failed, frame_count = 0, 0, len(frame_files)
        for filename in frame_files:
            if filename.lower().endswith(('.png', '.jpg', '.jpeg', '.gif', '.bmp')):
                try:
                    img_path = os.path.join(folder_path, filename)
                    img = Image.open(img_path)
                    images.append(img)
                    saved += 1
                    print(saved, flush=True)
                except Exception as e:
                    failed += 1
        if images:
            try:
                images[0].save(output_gif_name, save_all=True, append_images=images[1:], optimize=False, duration=int(delay), disposal=2, loop=0)
                return json.dumps({"status": "success", "data": [saved, failed]})
            except Exception as e:
                return json.dumps({"status": "failed", "message": f"Error saving GIF: {e}"})
        else:
            return json.dumps({"status": "failed", "message": "Error: could not find images to save as GIF"})
             
    except Exception as e:
        return json.dumps({"status": "failed", "message": f"Error: {e}"})

if __name__ == "__main__":
    if len(sys.argv) < 3:
        print(json.dumps({"status": "failed", "message": "Error: usage -> python save_gif.py <folder_path> <output_file_path> <gif_delay>"}))
        sys.exit(1)
    if not os.path.isdir(sys.argv[1]):
        print(json.dumps({"status": "failed", "message": f"Error: the specified path is not a directory: {sys.argv[1]}"}))
        sys.exit(1)
    result = save_images_as_gif(sys.argv[1], sys.argv[2], sys.argv[3])
    print(result)
