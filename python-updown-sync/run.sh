#!/bin/bash

docker run -it --rm --network host -e STORAGE_CONNECTION_STRING storageperf/python-updown-sync "$@"
