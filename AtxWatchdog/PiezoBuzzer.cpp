/**
 * PiezoBuzzer.cpp/PiezoBuzzer.h
 * Sends tones to an attached active piezobuzzer with specific intervals, without using hardware interrupts
 * 
 * @author Fabian Ramos R
 * @version 1.0
 * 
 * Requires constant calls to update() method.
 */

#include "PiezoBuzzer.h"


  PiezoBuzzer::PiezoBuzzer(int p_pin)
  {
    _pin = p_pin;  
  }
  
  void PiezoBuzzer::beep()
  {
    beep(beepLength);
  }
  
  void PiezoBuzzer::beep(int length)
  {
    _beepLengthActual = length;
    
    if (_beepOn)
      update();
    else
    {
      _beepOn = true;
      _beepStart = millis();
      if (!mute)
        digitalWrite(_pin, HIGH);
    }
  }
  
  void PiezoBuzzer::update()
  {    
    unsigned long m = millis();
    if (_beepStart > m || m > _beepStart + _beepLengthActual)
    {
        _beepOn = false;
        digitalWrite(_pin, LOW);
    }
  }

  bool PiezoBuzzer::isBeeping()
  {
    return _beepOn;
  }
