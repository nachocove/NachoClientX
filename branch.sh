#!/bin/sh

# USAGE: sh branch.sh [VERSION] [BUILD]
#
# It will create a branch named "branch_v[version]_[build]" and push it
# to Github so that the branch can be committed.

tag="v$1_$2"

source repos.sh

# Create a local branch for all repos
sh checkout_tag.sh "$tag"

# Push the branch to Github
branch="branch_$tag"
function gitPushBranch()
{
    pushd "../$1" 1> /dev/null
    echo "Push $branch.."
    git push origin "$branch"
    popd 1> /dev/null
}

for repo in $repos
do
    if [ "$repo" == "bc-csharp" ]; then
        continue
    fi
    gitPushBranch $repo
done
gitPushBranch NachoClientX