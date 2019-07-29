/**
 * AtxWatchdogDFU.cpp/AtxWatchdog.h
 * Provides a set of methods for connecting to the board using DFU mode
 * 
 * @author Fabian Ramos R
 * @version 1.0
 */

#ifndef ATXWATCHDOG_DFU_INCLUDED
#define ATXWATCHDOG_DFU_INCLUDED

bool  dfuCheck(int timeout);
void dfuMode();

#endif
