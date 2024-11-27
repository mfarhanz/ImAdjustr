from flask import Flask, request, jsonify
from PIL import Image
from io import BytesIO
from signal import SIGINT
from time import sleep
from waitress import serve
from threading import Thread
import numpy as np
import logging
import base64
import mmap
import sys
import os

SERVER_PID = os.getpid()
PORT = int(sys.argv[1])
app = Flask(__name__)

def shutdown_server():
    sleep(2)
    os.kill(os.getpid(), SIGINT)

@app.route('/shutdown', methods=['POST'])
def shutdown():
    Thread(target=shutdown_server).start()
    logger.info('Server shutting down')
    return jsonify(SERVER_PID), 200

@app.errorhandler(500)
def internal_error(error):
    logger.error(f"Internal Server Error: {error}")
    return jsonify({"output": "Internal Server Error"}), 500

@app.route('/test', methods=['GET'])
def connection_tester():
    return jsonify({"output": "Server active"}), 200

@app.route('/warmup', methods=['POST'])
def warmup():
    try:
        logger.info("Warming up connection link")
        # Test base64
        sample_data = b"korn"
        base64_encoded = base64.b64encode(sample_data)
        base64_decoded = base64.b64decode(base64_encoded)
        # Test mmap
        mmap_obj = mmap.mmap(-1, 100)
        mmap_obj.close()
        # Test BytesIO
        with BytesIO() as byte_stream:
            byte_stream.write(sample_data)
        # Test PIL Image
        with BytesIO() as img_data:
            img = Image.new('RGB', (1, 1))
            img.save(img_data, format='PNG')
            img.close()
        # Test NumPy
        np_array = np.zeros((10, 10))  # Create a small NumPy array
        return jsonify({"success": True, "output": "warm-up complete"}), 200
    except Exception as e:
        logger.error(f"Error:  {str(e)}")
        return jsonify({"success": False, "output": str(e)}), 500

@app.route('/process_image_through_path', methods=['POST'])
def process_image_through_filepath():
    try:
        data = request.json
        input_path = data.get('input_path')
        parameters = data['parameters']
        logger.info(f"Parameters: {input_path} | Parameters: {parameters}")
        image_array = np.array(Image.open(input_path))

        image_array[:] = 255 - image_array

        directory, filename = os.path.split(input_path)
        filename_no_ext, ext = os.path.splitext(filename)
        out_filename = f"{filename_no_ext}_out{ext}"
        out_path = os.path.join(directory, out_filename)
        img = Image.fromarray(image_array).save(out_path)
        logger.info(f"Saved: {out_path}")
        del img, image_array
        return jsonify({'success': True, 'output': out_path})
    except Exception as e:
        logger.error(f"Error:  {str(e)}")
        return jsonify({"success": False, "output": str(e)}), 500


@app.route('/process_image_direct', methods=['POST'])
def process_image_direct():         # can only be used when not sending output that is different in shape/size
    try:
        data = request.json
        width = data['width']
        height = data['height']
        bytes_per_pixel = data['bytes_per_pixel']
        parameters = data['parameters']
        base64_image = data['image']
        pixel_data = base64.b64decode(base64_image)
        logger.info(f"Data: {type(pixel_data)}({len(pixel_data)}) {width} {height} {bytes_per_pixel} | Parameters: {parameters}")
        image_array = np.frombuffer(pixel_data, dtype=np.uint8).reshape((width, height, bytes_per_pixel))

        image_array = 255 - image_array

        byte_array = image_array.tobytes()
        byte_io = BytesIO(byte_array)
        return byte_io.getvalue()
    except Exception as e:
        logger.error(f"Error:  {str(e)}")
        return jsonify({"success": False, "output": str(e)}), 500

@app.route('/process_image_through_mmap', methods=['POST'])
def process_image_through_mmap():
    try:
        data = request.json
        map_name = data['map_name']
        width = data['width']
        height = data['height']
        bytes_per_pixel = data['bytes_per_pixel']
        parameters = data['parameters']
        try:
            # Open memory-mapped file and treat it as a NumPy array directly
            mmapped_file = mmap.mmap(-1, width * height * bytes_per_pixel, tagname=map_name, access=mmap.ACCESS_WRITE)
            # Directly view memory-mapped file as a NumPy array (no need for frombuffer + reshape)
            image_array = np.frombuffer(mmapped_file, dtype=np.uint8).reshape((height, width, bytes_per_pixel))
            logger.info(f"Data: {map_name} {width} {height} {bytes_per_pixel} | Parameters: {parameters}")

            image_array[:] = 255 - image_array
            
            mmapped_file.flush()  # Ensure changes are written back to the memory-mapped file
            return jsonify({"success": True, "output": "Memory-mapped file updated"}), 200
        except Exception as e:
            logger.error(f"Error: {str(e)}")
            return jsonify({"success": False, "output": "Memory-mapped file not found"}), 404
    except Exception as e:
        logger.error(f"Error:  {str(e)}")
        return jsonify({"success": False, "output": str(e)}), 500

if __name__ == "__main__":
    log_dir = os.path.abspath(os.path.join(os.path.dirname( __file__ ), '..', '..', 'Logs'))
    if not os.path.exists(log_dir):
        os.makedirs(log_dir)
    logging.basicConfig(filename=f'{log_dir}\\flask.log', level=logging.DEBUG, filemode='w',
                        datefmt='%Y-%m-%d %H:%M:%S', format='%(asctime)s [%(levelname)s] %(funcName)s : %(message)s')
    logger = logging.getLogger(__name__)
    logger.info(f"Server running on localhost:{PORT}/ with Python {sys.version} ({sys.executable})")
    serve(app, host='127.0.0.1', port=PORT, threads= 8) # localhost
    logger.info("Server closed")