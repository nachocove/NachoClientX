# Checkout all repos to a specific tag. The tag must already exist.
# It should be created by mk_alpha.sh.
#
# USAGE: sh checkout tag.sh [tag]
#
# NOTE: If you add a new repo that is part of NachoClientX, you 
# MUST add that repo to repos.sh.

tag=$1

source repos.sh

function gitCheckoutTag()
{
    pushd "../$1" 1> /dev/null
	echo "Checkout $tag on $1..."
	branch="branch_$tag"
	git checkout master
	git branch -D $branch 2> /dev/null
	git checkout -b $branch $tag
	popd 1> /dev/null
}

for repo in $repos
do
  if [ "$repo" == "bc-csharp" ]; then
    continue
  fi
  gitCheckoutTag $repo
done
# repos.sh does not cover NachoClientX
gitCheckoutTag NachoClientX