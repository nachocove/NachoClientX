# Tag all repos with a tag and a message.
#
# USAGE: sh tag.sh [tag] [message]
#
# NOTE: If you add a new repo that is part of NachoClientX, you 
# MUST add that repo to repos.sh.

tag=$1
message=$2

source repos.sh

function gitTag()
{
    pushd "../$1" 1> /dev/null
	echo "Tagging `pwd` as $tag..."
	git tag -a "$tag" -m "$message"
	popd 1> /dev/null
}

for repo in $repos
do
  gitTag $repo
done
# repos.sh does not cover NachoClientX
gitTag NachoClientX