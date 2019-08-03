/**
 * AtxWatchdogDFU.cpp/AtxWatchdog.h
 * Provides a set of methods for connecting to the board using DFU mode
 * 
 * @author Fabian Ramos R
 * @version 1.0
 */

#include <Arduino.h>
#include "AtxWatchdogDFU.h"

#define DFU_MAGIC 0xEA

#define STATUS_READY 0xFA
#define STATUS_BUSY 0xFB
#define STATUS_WAITCMD 0xFC
#define STATUS_NOTIMPLEMENTED 0xFD
#define STATUS_EXECUTING 0xFE
#define STATUS_TERMINATED 0xFF
#define CLEARCONSOLE 0x11

// RIGHT SIDE
#define V12_SENSE A3
#define V5_SENSE A2
#define V5SB_SENSE A1
#define V3_3_SENSE A0

#define ADC_BANDGAP 1.0745f // Actual bandgap: 1.0745mV

bool dfuCheck(int timeout) {
  unsigned long startMillis = millis();
  
  int readBytes = 0;

  while ((millis() - startMillis) < timeout)
  {
      while (Serial.available() > 0)
      {
        int b = Serial.read();

        if (b == DFU_MAGIC) {
          // Received a DFU signal
          Serial.print("DFU");
          Serial.write(0x01); // Protocol version 1
          Serial.write(STATUS_BUSY); // Device busy

          delay(1000);

          while (Serial.available() > 0)
            Serial.read();

          return true; // Enter DFU mode
        }
    
        readBytes++;

        if (readBytes >= 128)
          return false; // too much garbage on the input stream
      }
  }

  return false; // Timeout
}

void dfuMode() {
  Serial.write(STATUS_READY); // Device Ready

  while (Serial.available() < 1) ;;
  byte b = Serial.read();

  switch (b) {
    case 1: // Debug method
      dfu_debug();
      break;
    case 5: // Bandgap calibration
      dfu_bandgap();
      break;
    case 6: // V12 Calibration
      dfu_calv12();
      break;
    case 7: // V5 calibration
      dfu_calv5();
      break;
    case 8: // V5SB calibration
      dfu_calv5sb();
      break;
    case 9: // V3.3 calibration
      dfu_calv3_3();
      break;
    case 10: // AREF Calibration
      dfu_calaref();
      break;
  }

  Serial.write(STATUS_TERMINATED); // DFU mode ended
  while (true);
}

void dfu_debug() {
  
}

void dfu_bandgap() {
  Serial.write(STATUS_EXECUTING);
  float samples[] = { 0, 0, 0, 0 };
  long sampleIndex = 0;
  pinMode(13, OUTPUT);
  digitalWrite(13, LOW);
  analogReference( INTERNAL );
  delay(1000);

  while (true) {
    float bg = dfu_thirdparty_getbandgap();
    samples[sampleIndex] = bg;
    sampleIndex++;
    
    String readName = F("A(0): ");
    String mvName = F(" mV");

    Serial.write(CLEARCONSOLE);
    Serial.print(readName);
    Serial.print(bg, 4);
    Serial.println(mvName);

    Serial.print(F("Mean "));
    Serial.print(readName);
    Serial.print(((samples[0] + samples[1] + samples[2] + samples[3]) / 4.0f), 4);
    Serial.println(mvName);
    
    if (sampleIndex >= 4) sampleIndex = 0;
    
    delay(1000);
  }
}

void dfu_calv12() {
  Serial.write(STATUS_EXECUTING);
  float samples[] = { 0, 0, 0, 0 };
  long sampleIndex = 0;
  float r1 = 9945;
  float r2 = 4640;

  while (true) {
    int val = analogRead(V12_SENSE);
    samples[sampleIndex] = val;
    sampleIndex++;

    long vcc = readVcc();

    String readName = F("A(12): ");
    String mvName = F(" mV");

    Serial.write(CLEARCONSOLE);
    Serial.print(F("Aref: "));
    Serial.print(vcc, DEC);
    Serial.println(mvName);
    Serial.print(readName);
    Serial.print(val, DEC);
    Serial.print(F(" / "));
    val = (val * vcc) / 1024.0f;
    Serial.print(val);
    Serial.print(mvName);
    
    float fval = (val * (r1+r2)) / r2;

    Serial.print(F(" -> "));
    Serial.print(fval, 4);
    Serial.println(mvName);
    
    float mean = (samples[0] + samples[1] + samples[2] + samples[3]) / 4.0f;

    Serial.print(F("Mean "));
    Serial.print(readName);
    Serial.print(mean, 4);
    Serial.print(F(" / "));
    mean = (mean * vcc) / 1024.0f;
    Serial.print(mean);
    Serial.print(mvName);

    fval = (mean * (r1+r2)) / r2;
    Serial.print(F(" -> "));
    Serial.print(fval, 4);
    Serial.println(mvName);
    
    if (sampleIndex >= 4) sampleIndex = 0;

    delay(500);
  }
}

void dfu_calv5() {
  Serial.write(STATUS_EXECUTING);
  float samples[] = { 0, 0, 0, 0 };
  long sampleIndex = 0;
  float r1 = 9915;
  float r2 = 21500;

  while (true) {
    int val = analogRead(V5_SENSE);
    samples[sampleIndex] = val;
    sampleIndex++;

    long vcc = readVcc();

    String readName = F("A(5): ");
    String mvName = F(" mV");

    Serial.write(CLEARCONSOLE);
    Serial.print(F("Aref: "));
    Serial.print(vcc, DEC);
    Serial.println(mvName);
    Serial.print(readName);
    Serial.print(val, DEC);
    Serial.print(F(" / "));
    val = (val * vcc) / 1024.0f;
    Serial.print(val);
    Serial.print(mvName);
    
    float fval = (val * (r1+r2)) / r2;

    Serial.print(F(" -> "));
    Serial.print(fval, 4);
    Serial.println(mvName);
    
    float mean = (samples[0] + samples[1] + samples[2] + samples[3]) / 4.0f;

    Serial.print(F("Mean "));
    Serial.print(readName);
    Serial.print(mean, 4);
    Serial.print(F(" / "));
    mean = (mean * vcc) / 1024.0f;
    Serial.print(mean);
    Serial.print(mvName);

    fval = (mean * (r1+r2)) / r2;
    Serial.print(F(" -> "));
    Serial.print(fval, 4);
    Serial.println(mvName);
    
    if (sampleIndex >= 4) sampleIndex = 0;

    delay(500);
  }
}

void dfu_calv5sb() {
  Serial.write(STATUS_EXECUTING);
  float samples[] = { 0, 0, 0, 0 };
  long sampleIndex = 0;
  float r1 = 9910;
  float r2 = 21600;

  while (true) {
    int val = analogRead(V5SB_SENSE);
    samples[sampleIndex] = val;
    sampleIndex++;

    long vcc = readVcc();

    String readName = F("A(5SB): ");
    String mvName = F(" mV");

    Serial.write(CLEARCONSOLE);
    Serial.print(F("Aref: "));
    Serial.print(vcc, DEC);
    Serial.println(mvName);
    Serial.print(readName);
    Serial.print(val, DEC);
    Serial.print(F(" / "));
    val = (val * vcc) / 1024.0f;
    Serial.print(val);
    Serial.print(mvName);
    
    float fval = (val * (r1+r2)) / r2;

    Serial.print(F(" -> "));
    Serial.print(fval, 4);
    Serial.println(mvName);
    
    float mean = (samples[0] + samples[1] + samples[2] + samples[3]) / 4.0f;

    Serial.print(F("Mean "));
    Serial.print(readName);
    Serial.print(mean, 4);
    Serial.print(F(" / "));
    mean = (mean * vcc) / 1024.0f;
    Serial.print(mean);
    Serial.print(mvName);

    fval = (mean * (r1+r2)) / r2;
    Serial.print(F(" -> "));
    Serial.print(fval, 4);
    Serial.println(mvName);
    
    if (sampleIndex >= 4) sampleIndex = 0;

    delay(500);
  }
}

void dfu_calv3_3() {
  Serial.write(STATUS_EXECUTING);
  float samples[] = { 0, 0, 0, 0 };
  long sampleIndex = 0;

  while (true) {
    int val = analogRead(V3_3_SENSE);
    samples[sampleIndex] = val;
    sampleIndex++;

    long vcc = readVcc();

    String readName = F("A(3.3): ");
    String mvName = F(" mV");

    Serial.write(CLEARCONSOLE);
    Serial.print(F("Aref: "));
    Serial.print(vcc, DEC);
    Serial.println(mvName);
    Serial.print(readName);
    Serial.print(val, DEC);
    Serial.print(F(" / "));
    val = (val * vcc) / 1024.0f;
    Serial.print(val);
    Serial.println(mvName);
    
    float mean = (samples[0] + samples[1] + samples[2] + samples[3]) / 4.0f;

    Serial.print(F("Mean "));
    Serial.print(readName);
    Serial.print(mean, 4);
    Serial.print(F(" / "));
    mean = (mean * vcc) / 1024.0f;
    Serial.print(mean);
    Serial.println(mvName);
    
    if (sampleIndex >= 4) sampleIndex = 0;

    delay(500);
  }
}

void dfu_calaref() {
  Serial.write(STATUS_EXECUTING);
  float samples[] = { 0, 0, 0, 0 };
  long sampleIndex = 0;

  while (true) {
    int val = readVcc();
    samples[sampleIndex] = val;
    sampleIndex++;

    String readName = F("A(Aref): ");
    String mvName = F(" mV");

    Serial.write(CLEARCONSOLE);
    Serial.print(readName);
    Serial.print(val, DEC);
    Serial.println(mvName);
    
    float mean = (samples[0] + samples[1] + samples[2] + samples[3]) / 4.0f;

    Serial.print(F("Mean "));
    Serial.print(readName);
    Serial.print(mean, 4);
    Serial.println(mvName);
    
    if (sampleIndex >= 4) sampleIndex = 0;

    delay(500);
  }
}

int dfu_thirdparty_getbandgap() {
   int val = analogRead(0);
   digitalWrite(13, HIGH);
}

long readVcc() {
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

  delay(5); // Wait for Vref to settle --
  ADCSRA |= _BV(ADSC); // Start conversion
  while (bit_is_set(ADCSRA,ADSC)); // measuring

  uint8_t low  = ADCL; // must read ADCL first - it then locks ADCH 
  uint8_t high = ADCH; // unlocks both

  long result = (high<<8) | low;

  // Calculate Vcc (in mV); 1100288 = 1.0745*1024*1000
  result = (ADC_BANDGAP * 1024L * 1000) / result; 
  return result; // Vcc in millivolts
}
