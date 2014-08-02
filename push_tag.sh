# Push a tag in all repos to remote server
#
# USAGE: sh push_tag.sh [tag]
#
# NOTE: If you add a new repo that is part of NachoClientX, you 
# MUST add that repo to repos.sh.

tag=$1

source repos.sh

function gitPushTag ()
{
    pushd "../$1" 1> /dev/null
	echo "Pushing tag $tag in `pwd`..."
	git push origin $tag
	popd 1> /dev/null
}

for repo in $repos
do
  gitPushTag $repo
done
# repos.sh does not cover NachoClientX
gitPushTag NachoClientX
