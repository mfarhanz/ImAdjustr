from numba import set_num_threads

set_num_threads(max([1, os.cpu_count()//2]))
