/**
 * AtxVoltmeter.cpp/AtxVoltmeter.h
 * Allows manipulation and signal analysis of ATX compatible PC PowerSupplies
 * 
 * @author Fabian Ramos R
 * @version 1.0
 */

#include "AtxVoltmeter.h"
#include "CalibrationCurves.c"

AtxVoltmeter::AtxVoltmeter(int p_v12_pin, int p_v5_pin, int p_v5sb_pin, int p_v3_3_pin, int p_ps_on_pin, int p_ps_on_trigger_pin, int p_pg_good_pin)
{
  v12_pin = p_v12_pin;
  v5_pin = p_v5_pin;
  v5sb_pin = p_v5sb_pin;
  v3_3_pin = p_v3_3_pin;
  
  pg_good_pin = p_pg_good_pin;
  ps_on_pin = p_ps_on_pin;
  ps_on_trigger_pin = p_ps_on_trigger_pin;

  sensing_sample_avg_count = 3;
  sensing_sample_trimming = true;
}

int AtxVoltmeter::avgAnalogRead(int pin)
{
  long val = 0;

  for (int i = 0; i < sensing_sample_avg_count; i++)
    val += analogRead(pin);

  if (sensing_sample_avg_count <= 1)
    return val;

  val /= sensing_sample_avg_count;

  if (val <= 341) // CURVE Trimming of values lower than 341
    return 0;

  return val;
}

float AtxVoltmeter::senseV12()
{
  int ival = avgAnalogRead(v12_pin);
  /*Serial.print("\tv12.ival: ");
  Serial.println(ival, DEC);*/
  
  float fval = calCurve4(v12x4, v12x3, v12x2, v12x1, v12sigma, ival);

  /*Serial.print("\tv12.fval: ");
  Serial.println(fval, DEC);

  Serial.print("\tCoeff^4: ");
  Serial.print(v12x4, DEC);
  Serial.print("\tCoeff^3: ");
  Serial.print(v12x3, DEC);
  Serial.print("\tCoeff^2: ");
  Serial.print(v12x2, DEC);
  Serial.print("\tCoeff^1: ");
  Serial.print(v12x1, DEC);
  Serial.print("\tsigma: ");
  Serial.println(v12sigma, DEC);*/

  if (fval < 0)
    return 0;

  return fval;
}

float AtxVoltmeter::senseV5()
{
  int ival = avgAnalogRead(v5_pin);

  float fval = ((float)ival * vcc) / 1023.0f;
  fval = calCurve4(v5x4, v5x3, v5x2, v5x1, v5sigma, fval);

  if (fval < 0)
    return 0;

  return fval;
}

float AtxVoltmeter::senseV5sb()
{
  int ival = avgAnalogRead(v5sb_pin);

  float fval = calCurve4(v5sbx4, v5sbx3, v5sbx2, v5sbx1, v5sbsigma, ival);

  if (fval < 0)
    return 0;

  return fval;
}

float AtxVoltmeter::senseV3_3()
{
  int ival = avgAnalogRead(v3_3_pin);
  
  float fval = calCurve4(v3_3x4, v3_3x3, v3_3x2, v3_3x1, v3_3sigma, ival); // (?) No curve since its directly connected to the Microcontroller
  //float fval = ((float)ival * vcc) / 1023.0f;

  if (fval < 0)
    return 0;

  return fval;
}

float AtxVoltmeter::V12()
{
  return v12;
}

float AtxVoltmeter::V5()
{
  return v5;
}

float AtxVoltmeter::V5SB()
{
  return v5sb;
}

float AtxVoltmeter::V3_3()
{
  return v3_3;
}

void AtxVoltmeter::update()
{
   vcc = readVcc() / 1000.0f;
   
   v12 = senseV12();   
   v5 = senseV5();   
   v5sb = senseV5sb();   
   v3_3 = senseV3_3();
   
   ps_on = digitalRead(ps_on_pin);
   pg_good = digitalRead(pg_good_pin);
}

bool AtxVoltmeter::isPgGoodPresent()
{
  return pg_good > 0;
}

bool AtxVoltmeter::isPsuPresent()
{
  return (isV5sbPresent() || isPsOnPresent());
}

bool AtxVoltmeter::isV5sbPresent()
{
  return (v5sb > 1.0f);
}

bool AtxVoltmeter::isPsOnPresent()
{
  return (ps_on > 0);
}

bool AtxVoltmeter::isOn()
{
  return !isPsOnPresent() && isV5sbPresent();
}

bool AtxVoltmeter::isTriggered()
{
  return (ps_on_trigger > 0);
}

int AtxVoltmeter::getSamplingAvgCount()
{
  return sensing_sample_avg_count;
}

void AtxVoltmeter::setSamplingAvgCount(int value)
{
  sensing_sample_avg_count = value;
}

bool AtxVoltmeter::getSamplingCurveTrimming()
{
  return sensing_sample_trimming;
}

void AtxVoltmeter::setSamplingCurveTrimming(bool value)
{
  sensing_sample_trimming = value;
}

void AtxVoltmeter::setV12Coefficients(float x4, float x3, float x2, float x1, float sigma)
{
  v12x4 = x4;
  v12x3 = x3;
  v12x2 = x2;
  v12x1 = x1;
  v12sigma = sigma;
}

void AtxVoltmeter::setV5Coefficients(float x4, float x3, float x2, float x1, float sigma)
{
  v5x4 = x4;
  v5x3 = x3;
  v5x2 = x2;
  v5x1 = x1;
  v5sigma = sigma;
}

void AtxVoltmeter::setV5sbCoefficients(float x4, float x3, float x2, float x1, float sigma)
{
  v5sbx4 = x4;
  v5sbx3 = x3;
  v5sbx2 = x2;
  v5sbx1 = x1;
  v5sbsigma = sigma;
}

void AtxVoltmeter::setV3_3Coefficients(float x4, float x3, float x2, float x1, float sigma)
{
  v3_3x4 = x4;
  v3_3x3 = x3;
  v3_3x2 = x2;
  v3_3x1 = x1;
  v3_3sigma = sigma;
}

long AtxVoltmeter::readVcc() {
  // Read 1.1V reference against AVcc
  // set the reference to Vcc and the measurement to the internal 1.1V reference
  #if defined(__AVR_ATmega32U4__) || defined(__AVR_ATmega1280__) || defined(__AVR_ATmega2560__)
    ADMUX = _BV(REFS0) | _BV(MUX4) | _BV(MUX3) | _BV(MUX2) | _BV(MUX1);
  #elif defined (__AVR_ATtiny24__) || defined(__AVR_ATtiny44__) || defined(__AVR_ATtiny84__)
    ADMUX = _BV(MUX5) | _BV(MUX0);
  #elif defined (__AVR_ATtiny25__) || defined(__AVR_ATtiny45__) || defined(__AVR_ATtiny85__)
    ADMUX = _BV(MUX3) | _BV(MUX2);
  #else
    ADMUX = _BV(REFS0) | _BV(MUX3) | _BV(MUX2) | _BV(MUX1);
  #endif 

  delayMicroseconds(500); // Wait for Vref to settle -- Was 2ms @ KDERazorback
  ADCSRA |= _BV(ADSC); // Start conversion
  while (bit_is_set(ADCSRA,ADSC)); // measuring

  uint8_t low  = ADCL; // must read ADCL first - it then locks ADCH 
  uint8_t high = ADCH; // unlocks both

  long result = (high<<8) | low;

  // Calculate Vcc (in mV); 1125300 = 1.1*1023*1000
  // Actual bandgap: 1100748 = 1.076V*1023*1000
  result = 1100748L / result; 
  return result; // Vcc in millivolts
}

void AtxVoltmeter::turnOn() {
  digitalWrite(ps_on_trigger_pin, HIGH);
}

void AtxVoltmeter::turnOff() {
  digitalWrite(ps_on_trigger_pin, LOW);
}
