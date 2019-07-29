/**
 * Timer.cpp/Timer.h
 * Executes methods at specified intervals without the use of hardware interrupts.
 * 
 * @author Fabian Ramos R
 * @version 1.0
 * 
 * Requires constant calls to update() method.
 */
 
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
