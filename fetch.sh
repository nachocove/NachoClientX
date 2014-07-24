# Fetch all sources from git
#
# USAGE: sh fetch.sh
#
# NOTE: If you add a new repo that is part of NachoClientX, you 
# MUST add that repo to repos.sh.

source repos.sh

function gitUpdate()
{
    pushd .. 1> /dev/null
    if [ ! -d "$1" ]; then
        echo "Cloning $1..."
        git clone git@github.com:nachocove/"$1".git
        if [  $? -ne 0 ]; then
            echo FAILED git clone git@github.com:nachocove/"$1".git
            exit $?
        fi
    fi
    pushd "$1" 1> /dev/null
    echo "Updating $1..."
    git pull 
    if [ $? -ne 0 ]; then
        echo FAILED git pull "$1"
        exit $?
    fi
    popd 1> /dev/null
    popd 1> /dev/null
}

for repo in $repos
do
  gitUpdate $repo
  if [ "$repo" == "bc-csharp" ]; then
    # Switch to visual-studio-2010 branch
    pushd ../bc-csharp; git checkout -b visual-studio-2010 origin/visual-studio-2010; popd
  fi
done
