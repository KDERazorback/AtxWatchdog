/**
 * AtxVoltmeter.cpp/AtxVoltmeter.h
 * Allows manipulation and signal analysis of ATX compatible PC PowerSupplies
 * 
 * @author Fabian Ramos R
 * @version 1.1
 */

#ifndef ATXVOLTMETER_INCLUDED
#define ATXVOLTMETER_INCLUDED

#include <Arduino.h>

class AtxVoltmeter {
  private:
  // Pins
  int v12_pin;
  int v5_pin;
  int v5sb_pin;
  int v3_3_pin;
  int ps_on_pin;
  int pg_good_pin;
  int ps_on_trigger_pin;
  int sensing_sample_avg_count;
  bool _hiNoiseMode = false;
  
  // Last measured Volts (float)
  float v12;
  float v5;
  float v5sb;
  float v3_3;
  float vcc;
  long avccref = 0;
  unsigned long last_avccref_update = 0;

  // Status
  int ps_on;
  int pg_good;
  int ps_on_trigger;

  // Methods
  int avgAnalogRead(int pin);

  public:
  AtxVoltmeter(int p_v12_pin, int p_v5_pin, int p_v5sb_pin, int p_v3_3_pin, int p_ps_on_pin, int p_ps_on_trigger_pin, int p_pg_good_pin);

  float senseV12();
  
  float senseV5();
  
  float senseV5sb();
  
  float senseV3_3();

  bool isPsuPresent();

  bool isV5sbPresent();

  bool isPsOnPresent();

  bool isPgGoodPresent();

  bool isOn();

  float V12();

  float V5();

  float V5SB();

  float V3_3();

  void turnOn();

  bool isTriggered();

  void turnOff();

  int getSamplingAvgCount();

  void setSamplingAvgCount(int value);

  void update();

  long readVcc();

  void setHiNoiseMode(bool value);

  bool hiNoiseMode();
};

#endif
