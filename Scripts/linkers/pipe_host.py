import sys
import os
import mmap
import msvcrt
import struct
import logging
import numpy as np
from time import sleep

scripts_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if scripts_dir not in sys.path:
    sys.path.insert(1, scripts_dir)

def run_pipe_listener():
    # Set the console to binary mode
    msvcrt.setmode(sys.stdin.fileno(), os.O_BINARY)
    while True:
        # Read the headers first
        try:
            type_byte_discard = sys.stdin.buffer.read(1)  # ignore type byte since first value in header is always process mode string
            header_start_length = struct.unpack('i', sys.stdin.buffer.read(4))[0]
            header_start_val = sys.stdin.buffer.read(header_start_length).decode('utf-8')
            logger.info(f"header: {header_start_val}")
            if header_start_val == 'MMAP':
                process_image_through_mmap()
            elif header_start_val == 'DIRECT':
                process_image_direct()
            elif header_start_val == 'SHTD':
                logger.info(f"STREAM CLOSING | HEADER: Shutdown issued ({header_start_val})")
                sys.stdout.flush()
                break;
        except Exception as e:
            logger.error(f"{e}")
            sys.stdout.flush()
            sleep(0.5)  # Wait for input
            continue

def get_piped_data():
    parameters = []
    data_inconsistent = False
    type_byte_discard = sys.stdin.buffer.read(1) # ignore type byte since second value in header is always the number of parameters after it
    parameter_count = struct.unpack('i', sys.stdin.buffer.read(4))[0]
    for _ in range(parameter_count):
        try:
            # Read the type identifier
            type_byte = sys.stdin.buffer.read(1)
            if not type_byte:
                break   # End of input
            if type_byte == b'\x01':  # Integer
                parameter = struct.unpack('i', sys.stdin.buffer.read(4))[0]
            elif type_byte == b'\x02':  # Float
                parameter = struct.unpack('f', sys.stdin.buffer.read(4))[0]
            elif type_byte == b'\x03':  # Double
                parameter = struct.unpack('d', sys.stdin.buffer.read(8))[0]
            elif type_byte == b'\x04':  # Boolean
                parameter = struct.unpack('?', sys.stdin.buffer.read(1))[0]
            elif type_byte == b'\x05':  # Char
                parameter = sys.stdin.buffer.read(1).decode('utf-8')
            elif type_byte in [b'\x06', b'\x07']:  # String or Byte[]
                length_bytes = struct.unpack('i', sys.stdin.buffer.read(4))[0]
                parameter = sys.stdin.buffer.read(length_bytes)
                if len(parameter) != length_bytes:
                    data_inconsistent = True  # Data missing or inconsistent
                if type_byte == b'\x06':
                    parameter = parameter.decode('utf-8')
            else:
                continue  # Unknown type, skip
            parameters.append(parameter)
            if data_inconsistent:
                break  # Stop taking parameters if part of data is missing
        except Exception as e:
            logger.error(f"Error parsing parameter data: {str(e)}")
            break
    return parameters

def process_image_through_mmap():
    try:
        # Read the headers first
        data = get_piped_data()
        map_name = data[0]
        width = data[1]
        height = data[2]
        bytes_per_pixel = data[3]
        if not any([map_name, width, height, bytes_per_pixel]):
            sys.stdout.buffer.write((0).to_bytes(1, byteorder='little'))
            return
        try:
            # Open memory-mapped file and treat it as a NumPy array directly
            mmapped_file = mmap.mmap(-1, width * height * bytes_per_pixel, tagname=map_name, access=mmap.ACCESS_WRITE)
            # Directly view memory-mapped file as a NumPy array (no need for frombuffer + reshape)
            image_array = np.frombuffer(mmapped_file, dtype=np.uint8).reshape((height, width, bytes_per_pixel))

            image_array[:] = image_array + 5
            
            mmapped_file.flush()  # Ensure changes are written back to the memory-mapped file
            sys.stdout.buffer.write((1).to_bytes(1, byteorder='little'))
        except Exception as e:
            logger.error(f"Error: {str(e)}")
            sys.stdout.buffer.write((0).to_bytes(1, byteorder='little'))
        sys.stdout.flush()  # Ensure all data is sent
        logger.info(f"PIPED | Data: {map_name} {width} {height} {bytes_per_pixel} | Parameters: {data[4:]}")
        del image_array
    except Exception as e:
        logger.error(f"{e}")
        sys.stdout.buffer.write((0).to_bytes(1, byteorder='little'))
        return

def process_image_direct():
    try:
        data = get_piped_data()
        width = data[0]
        height = data[1]
        bytes_per_pixel = data[2]
        image_data = data[3]
        if not any([image_data, width, height, bytes_per_pixel]):
            sys.stdout.buffer.write((0).to_bytes(1, byteorder='little'))
            return
        image_array = np.copy(np.frombuffer(image_data, dtype=np.uint8)).reshape((height, width, bytes_per_pixel))

        image_array += 5
        
        processed_image_data = image_array.tobytes()
        sys.stdout.buffer.write((1 if len(processed_image_data) == len(image_data) else 0).to_bytes(1, byteorder='little'))
        sys.stdout.buffer.write(processed_image_data)
        sys.stdout.flush()  # Ensure all data is sent
        logger.info(f"PIPED | Data: {len(processed_image_data)} {width} {height} {bytes_per_pixel} | Parameters: {data[4:]}")
        del image_data, image_array, processed_image_data
    except Exception as e:
        logger.error(f"{e}")
        sys.stdout.buffer.write((0).to_bytes(1, byteorder='little'))
        return

if __name__ == "__main__":
    log_dir = os.path.abspath(os.path.join(os.path.dirname( __file__ ), '..', '..', 'Logs'))
    if not os.path.exists(log_dir):
        os.makedirs(log_dir)
    logging.basicConfig(level=logging.DEBUG, format='%(asctime)s [%(levelname)s] %(funcName)s : %(message)s',
                            datefmt='%Y-%m-%d %H:%M:%S', filename=f'{log_dir}\\pipe_py.log', filemode='w')
    logger = logging.getLogger(__name__)
    logger.info(f"Pipe client (mmap) running with Python {sys.version} ({sys.executable})")
    run_pipe_listener()
    logger.info("STREAM END")
