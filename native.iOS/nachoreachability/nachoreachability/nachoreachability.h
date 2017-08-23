//
//  nachoreachability.h
//  nachoreachability
//
//  Created by Owen Shaw on 8/23/17.
//  Copyright © 2017 Nacho Cove, Inc. All rights reserved.
//


typedef void (__stdcall *ReachabilityCallback)();

void nacho_internet_reachability_init(ReachabilityCallback callback);
void nacho_internet_reachability_start_notifier();
void nacho_internet_reachability_stop_notifier();
int nacho_internet_reachability_is_reachable();
int nacho_internet_reachability_is_reachable_via_wifi();
