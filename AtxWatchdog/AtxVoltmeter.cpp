/**
 * AtxVoltmeter.cpp/AtxVoltmeter.h
 * Allows manipulation and signal analysis of ATX compatible PC PowerSupplies
 * 
 * @author Fabian Ramos R
 * @version 1.1
 */

#include "AtxVoltmeter.h"

#define AREF_MEASURE_LIFETIME 300 // Specifies the amount of time that an AREF value is considered valid for ADC conversions (in ms)
#define ADC_BANDGAP 1.0745f // Actual bandgap: 1.0745mV

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
}

int AtxVoltmeter::avgAnalogRead(int pin)
{
  long val = 0;

  analogRead(pin);
  delayMicroseconds(50);

  if (_hiNoiseMode) {
    analogRead(pin);
    delayMicroseconds(50);
  }
  
  for (int i = 0; i < sensing_sample_avg_count; i++)
    val += analogRead(pin);

  if (sensing_sample_avg_count > 1)
    val /= sensing_sample_avg_count;

  return val;
}

float AtxVoltmeter::senseV12()
{
  int ival = avgAnalogRead(v12_pin);

  float fval = ((float)ival * vcc) / 1023.0f;
  
  float r1 = 9945;
  float r2 = 4640;

  fval = (fval * (r1+r2)) / r2;

  return fval;
}

float AtxVoltmeter::senseV5()
{
  int ival = avgAnalogRead(v5_pin);

  float fval = ((float)ival * vcc) / 1023.0f;
  
  float r1 = 9915;
  float r2 = 21500;

  fval = (fval * (r1+r2)) / r2;

  return fval;
}

float AtxVoltmeter::senseV5sb()
{
  int ival = avgAnalogRead(v5sb_pin);

  float fval = ((float)ival * vcc) / 1023.0f;

  float r1 = 9910;
  float r2 = 21600;

  fval = (fval * (r1+r2)) / r2;

  return fval;
}

float AtxVoltmeter::senseV3_3()
{
  int ival = avgAnalogRead(v3_3_pin);
  
  float fval = ((float)ival * vcc) / 1023.0f;

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

   v3_3 = senseV3_3();
   v5sb = senseV5sb();  
   v5 = senseV5(); 
   v12 = senseV12();   
   
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

long AtxVoltmeter::readVcc() {
  if (millis() - last_avccref_update > AREF_MEASURE_LIFETIME) avccref = 0;
  
  if (avccref > 0) return avccref;
  
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

  delayMicroseconds(750); // Wait for Vref to settle -- Was 2ms @ KDERazorback
  ADCSRA |= _BV(ADSC); // Start conversion
  while (bit_is_set(ADCSRA,ADSC)); // measuring

  uint8_t low  = ADCL; // must read ADCL first - it then locks ADCH 
  uint8_t high = ADCH; // unlocks both

  long result = (high<<8) | low;

  // Calculate Vcc (in mV); 1100288 = 1.0745*1024*1000
  result = (ADC_BANDGAP * 1024L * 1000) / result; 
  avccref = result;
  last_avccref_update = millis();
  return result; // Vcc in millivolts
}

void AtxVoltmeter::turnOn() {
  digitalWrite(ps_on_trigger_pin, HIGH);
}

void AtxVoltmeter::turnOff() {
  digitalWrite(ps_on_trigger_pin, LOW);
}

void AtxVoltmeter::setHiNoiseMode(bool value) {
  _hiNoiseMode = value;
}

bool AtxVoltmeter::hiNoiseMode() {
  return _hiNoiseMode;
}
