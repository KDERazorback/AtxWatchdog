/**
 * AtxVoltmeter.cpp/AtxVoltmeter.h
 * Allows manipulation and signal analysis of ATX compatible PC PowerSupplies
 * 
 * @author Fabian Ramos R
 * @version 1.0
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
  bool sensing_sample_trimming;

  // Calibration curves values
  float v12x4;
  float v12x3;
  float v12x2;
  float v12x1;
  float v12sigma;

  float v5x4;
  float v5x3;
  float v5x2;
  float v5x1;
  float v5sigma;

  float v5sbx4;
  float v5sbx3;
  float v5sbx2;
  float v5sbx1;
  float v5sbsigma;

  float v3_3x4;
  float v3_3x3;
  float v3_3x2;
  float v3_3x1;
  float v3_3sigma;
  
  // Last measured Volts (float)
  float v12;
  float v5;
  float v5sb;
  float v3_3;
  float vcc;

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

  bool getSamplingCurveTrimming();

  void setSamplingCurveTrimming(bool value);

  void setV12Coefficients(float x4, float x3, float x2, float x1, float sigma);
  void setV5Coefficients(float x4, float x3, float x2, float x1, float sigma);
  void setV5sbCoefficients(float x4, float x3, float x2, float x1, float sigma);
  void setV3_3Coefficients(float x4, float x3, float x2, float x1, float sigma);

  void update();

  long readVcc();
};

#endif
