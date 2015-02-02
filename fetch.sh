# Fetch all sources from git
#
# USAGE: sh fetch.sh
#
# NOTE: If you add a new repo that is part of NachoClientX, you 
# MUST add that repo to repos.sh.

source repos.sh
./fetch.py $repos
