/**
 * PiezoBuzzer.cpp/PiezoBuzzer.h
 * Sends tones to an attached active piezobuzzer with specific intervals, without using hardware interrupts
 * 
 * @author Fabian Ramos R
 * @version 1.0
 * 
 * Requires constant calls to update() method.
 */

#ifndef PIEZOBUZZER_INCLUDED
#define PIEZOBUZZER_INCLUDED

#include <Arduino.h>

class PiezoBuzzer
{
  private:
  int _pin = 0;
  unsigned long _beepStart = 0;
  long _beepLengthActual;
  bool _beepOn = false;

  public:
  bool mute = false;
  int beepLength = 75;
  
  PiezoBuzzer(int p_pin);
  
  void beep();
  
  void beep(int length);
  
  void update();

  bool isBeeping();
};

#endif
