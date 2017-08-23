//
//  nachoreachability.m
//  nachoreachability
//
//  Created by Owen Shaw on 8/23/17.
//  Copyright © 2017 Nacho Cove, Inc. All rights reserved.
//

#import "nachoreachability.h"
#import "Reachability.h"

static Reachability *Internet = nil;

void nacho_internet_reachability_init(ReachabilityCallback callback)
{
    Internet = [Reachability reachabilityForInternetConnection];
    if (callback != NULL){
        Internet.reachableBlock = ^(Reachability *_){
            callback();
        };
        Internet.unreachableBlock = ^(Reachability *_) {
            callback();
        };
    }
}

void nacho_internet_reachability_start_notifier()
{
    [Internet startNotifier];
}

void nacho_internet_reachability_stop_notifier()
{
    [Internet stopNotifier];
}

int nacho_internet_reachability_is_reachable()
{
    return [Internet isReachable];
}

int nacho_internet_reachability_is_reachable_via_wifi()
{
    return [Internet isReachableViaWiFi];
}
