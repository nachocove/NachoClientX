# Checkout all repos to a specific branch. The branch must already exist.
# It should be created by branch.sh.
#
# USAGE: sh checkout_branch.sh [branch]
#

branch=$1

source repos.sh

function gitCheckoutBranch()
{
    pushd "../$1" 1> /dev/null
	echo "Checkout $branch on $1..."
	git checkout $branch
	popd 1> /dev/null
}

for repo in $repos
do
  if [ "$repo" == "bc-csharp" ]; then
    continue
  fi
  gitCheckoutBranch $repo
done
# repos.sh does not cover NachoClientX
gitCheckoutBranch NachoClientX