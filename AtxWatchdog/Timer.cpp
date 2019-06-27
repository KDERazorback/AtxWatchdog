/**
 * Timer.cpp/Timer.h
 * Executes methods at specified intervals without the use of hardware interrupts.
 * 
 * @author Fabian Ramos R
 * @version 1.0
 * 
 * Requires constant calls to update() method.
 */

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
