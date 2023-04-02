# Windows Privilege Escalation Exploit
#### A tool that demonstrates a privilege escalation attack on Windows, allowing a normal user to escalate to SYSTEM NT AUTHORITY privileges.


This exploit consists of the following stages:

- Load a DLL containing the shellcode.
- Inject the shellcode into Notepad, which runs with high privileges to respond to PPL (Protected Process Light).
- Establish an internal C# shell using a memory-mapped file for communication.
- Execute commands read from the memory space of the file TESTPRIVESC by the injected shellcode.
- Share the output of the commands in the shellcode's memory space for further processing.
### Features
This program combines various techniques to demonstrate a successful privilege escalation attack, including:

- DLL injection
- Reverse shell
- UAC (User Account Control) bypass to SYSTEM
- Process injection
-  Fileless shell using memory-mapped file
### Requirements
- Windows operating system (tested on Windows 10)
- .NET Framework
### Usage
- Download the source code or precompiled binaries.
- Open a command prompt or PowerShell with normal user privileges.
Run the program with the following command: "TestPrivEsc.exe"
- The program will execute the privilege escalation attack, and if successful, you will be presented with a C# shell with SYSTEM NT AUTHORITY privileges.
### Disclaimer
This project is intended for educational and research purposes only. The authors of this project are not responsible for any misuse of the provided code or any consequences resulting from its usage. Use at your own risk.
