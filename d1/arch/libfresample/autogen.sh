#!/bin/sh
set -e

python3 tests/genmake.py --autoconf
python3 genmake.py
aclocal
autoconf
