#!/bin/sh

xbuild /p:Configuration=Release "/p:Platform=Any CPU" OpenVpnService.sln 
if [ "$?" != "0" ]; then exit 1; fi
xbuild /p:Configuration=Release "/p:Platform=x86" OpenVpnService.sln 
if [ "$?" != "0" ]; then exit 1; fi
xbuild /p:Configuration=Release "/p:Platform=x64" OpenVpnService.sln 
if [ "$?" != "0" ]; then exit 1; fi

xbuild /p:Configuration=Debug "/p:Platform=Any CPU" OpenVpnService.sln 
if [ "$?" != "0" ]; then exit 1; fi
xbuild /p:Configuration=Debug "/p:Platform=x86" OpenVpnService.sln 
if [ "$?" != "0" ]; then exit 1; fi
xbuild /p:Configuration=Debug "/p:Platform=x64" OpenVpnService.sln 
if [ "$?" != "0" ]; then exit 1; fi
