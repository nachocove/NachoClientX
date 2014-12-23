# Copyright 2014, NachoCove, Inc
from threading import Lock
mutex = Lock()

def get_next_counter_value():
    """
    Simulate a 64bit counter by saving it to a file. Since python long-ints
    are of infinite length, let's simulate a rollover simply by checking the length
    of the long converted to a base10 string. If the length is > 19, we've rolled
    over. In C, we probably can just use an unsigned long, and check for it to
    be 0, instead of the (faulty) string-length check.

    :return: the next value from the counter, with mutex protection.
    :rtype: int
    """
    global mutex
    counter_filename = "counter.txt"
    mutex.acquire()
    try:
        try:
            counter = long(open(counter_filename, 'r').read().strip('\n')) + 1
            if len(str(counter)) > 19:
                raise Exception('Counter rolled over! Need to have a new key.')
        except IOError:
            counter = 0
        open(counter_filename, 'w').write(str(counter))
    finally:
        mutex.release()
    return counter


def Nacho_GCM_IV(device_id, counter):
    """
    Create an IV suitable for GCM. In GCM, it is fatal to reuse the same IV with a given key.

    "The probability that the authenticated encryption function ever will be invoked with the
    same IV and the same key on two (or more) distinct sets of input data shall be no greater
    than 2^-32."

    In IPsec, using the recommendation is OK, since we can rekey. In our case, we have files laying around
    in s3 for a long time. So we want to be make SURE the IV never repeats.

    To protect against this, we use various pieces of information:

    * The device ID. Pretty good indication that we're on different devices, but not *guaranteed* to be different
    * A counter protected by a mutex

    For this to fail the uniqueness test, we would have to have an encryption happen on two devices that somehow
    managed to get the same device ID, at the exact same time (in milliseconds) and have had the same number of
    encryptions previously (i.e. to make the counter the same).

    :param device_id: the nacho device ID
    :type device_id: str
    :return: an IV, base64 encoded
    :rtype: str
    """
    import os
    # <device-id>:<timestamp>:<counter protected by mutex>:<random stuff>
    # 19 digits is the size of the string for 2**63 -1, which is a 64bit integer.
    IV = ":".join((device_id, "{:019d}".format(counter)))
    random_length = 160-len(IV)
    #print "IV: %s:<random crap, %d bytes>" % (IV, random_length)
    IV = IV+":"+os.urandom(random_length)
    return IV


if __name__ == "__main__":
    device_id = 'Ncho3168E1E2XF59EX4E37XAFDEX'
    counter = get_next_counter_value()
    print Nacho_GCM_IV(device_id, counter).encode('base-64')

