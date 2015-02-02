source repos.sh
./fetch.py $repos
git update-index --assume-unchanged NachoClient.userprefs
make -f build.mk
