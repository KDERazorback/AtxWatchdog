/**
 * AtxWatchdogDFU.cpp/AtxWatchdog.h
 * Provides a set of methods for connecting to the board using DFU mode
 * 
 * @author Fabian Ramos R
 * @version 1.0
 */

#include <Arduino.h>

bool dfuCheck(int timeout) {
  unsigned long startMillis = millis();
  
  int readBytes = 0;

  while ((millis() - startMillis) < timeout)
  {
      while (Serial.available() > 0)
      {
        int b = Serial.read();
        if (b < 0) break;

        if (b == 234) {
          // Received a DFU signal (char code 234 in DEC)
          Serial.print("DFU");
          Serial.write(0x01); // Protocol version 1
          Serial.write(0xfb); // Device busy

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
  while (true);
}
