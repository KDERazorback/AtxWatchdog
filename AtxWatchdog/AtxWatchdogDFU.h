/**
 * AtxWatchdogDFU.cpp/AtxWatchdog.h
 * Provides a set of methods for connecting to the board using DFU mode
 * 
 * @author Fabian Ramos R
 * @version 1.0
 */

#ifndef ATXWATCHDOG_DFU_INCLUDED
#define ATXWATCHDOG_DFU_INCLUDED

bool dfuCheck(int timeout);
void dfuMode();

// DFU methods
void dfu_debug();
void dfu_bandgap();
void dfu_calv12();
void dfu_calv5();
void dfu_calv5sb();
void dfu_calv3_3();
void dfu_calaref();

int dfu_thirdparty_getbandgap();
long readVcc();
#endif
