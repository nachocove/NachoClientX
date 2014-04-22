class ObjectStack:
    """
    Generic stack. Used for XML parsing.
    """
    def __init__(self):
        self.stack = []
        self._max_depth = 0

    def push(self, obj):
        assert obj is not None
        self.stack.insert(0, obj)
        cur_depth = self.depth()
        if cur_depth > self._max_depth:
            self._max_depth = cur_depth

    def pop(self):
        if self.is_empty():
            return None
        return self.stack.pop(0)

    def is_empty(self):
        return len(self.stack) == 0

    def peek(self):
        if self.is_empty():
            return None
        return self.stack[0]

    def depth(self):
        return len(self.stack)

    def max_depth(self):
        return self._max_depth