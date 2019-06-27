#include <Arduino.h>
#include "Timer.h"

Timer::Timer(void (*p_callback)(void))
{
  callback = p_callback;
}

void Timer::update()
{
  unsigned long m = millis();
  if (lastInvokeAt > m || m > lastInvokeAt + interval)
      fire(m);
}

void Timer::fire()
{
  fire(millis());
}

void Timer::fire(long m)
{
  lastInvokeAt = m;
  callback();
}
