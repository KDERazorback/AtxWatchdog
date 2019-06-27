#ifndef TIMER_INCLUDED
#define TIMER_INCLUDED

class Timer {
  public:
  unsigned long interval = 1000;
  unsigned long lastInvokeAt = 0;
  
  Timer(void (*p_callback)(void));

  void update();
  void fire();
  void fire(long m);
  void (*callback)(void);
};

#endif
