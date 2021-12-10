# InMemory_RAMFileSystem_Windows

A windows Filesystem that creates a virtual disk on your RAM. This disk is accessible as a regular drive on windows.

This Filesystem is based off Dokan (https://github.com/dokan-dev/dokany) which is a file system filter driver to develop file systems in userspace.

The structure of the file system is quite simple. It uses basic trees for creating a directory heirarchy and a hashmap to store blocks. The directory itself is repreented in a hashmap.

I use this filesystem for fast file access for some of my workloads. You cannot expect speeds in the Gigabyte range. You will most likely see SSD type speeds. I use this on a machine that has SATA harddisk drives and I need a temporary cache drive which is fast.

Of course, you are still limited by the amount of RAM on your system. There is no runtime checks and writing too much of data can cause your system to slow down. 

Use this project to start learning the basics of filesystem or modify this code to suit your needs.
