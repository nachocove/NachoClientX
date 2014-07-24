# Delete a tag.
#
# USAGE: sh untag.sh [tag]
#
# NOTE: If you add a new repo that is part of NachoClientX, you 
# MUST add that repo to repos.sh.

tag=$1

source repos.sh

function gitUntag()
{
    pushd "../$1" 1> /dev/null
	echo "Deleting tag $tag in `pwd`..."
	git tag -d $tag
	popd 1> /dev/null
}

for repo in $repos
do
  gitUntag $repo
done
# repos.sh does not cover NachoClientX
gitUntag NachoClientX
