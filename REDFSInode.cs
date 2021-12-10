using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DokanNet;

namespace REDFS_ClusterMode
{
    class REDFSInode
    {
        //All file attributes and inode type for both file/dirs.
        public FileInformation fileInfo;
        public string parentDirectory;
        public Boolean isDirty;
        public int BLK_SIZE = 4096;

        IDictionary inCoreData = new Dictionary<int, byte[]>();

        //Flags specific for directories
        public List<string> items = new List<string>(); //all files/dir names
        public Boolean isInodeSkeleton = false;


        public REDFSInode(Boolean isDirectory, string parent, string name)
        {
            fileInfo = new FileInformation();
            fileInfo.FileName = name;
            if (isDirectory) {
                fileInfo.Attributes |= System.IO.FileAttributes.Directory;
            } else {
                fileInfo.Attributes |= System.IO.FileAttributes.Normal;
            }

            fileInfo.Length = 0;
           
            fileInfo.CreationTime = DateTime.Now;
            fileInfo.LastAccessTime = DateTime.Now;
            fileInfo.LastWriteTime = DateTime.Now;

            parentDirectory = parent;
        }

        public void InitializeDirectory(List<string> children)
        {
            if (isDirectory())
            {
                isInodeSkeleton = false;
            }
            else
            {
                throw new SystemException();
            }
        }

        public void InitializeFile(byte[] fsInfoBlock)
        {
            if (!isDirectory())
            {
                isInodeSkeleton = false;
            }
            else
            {
                throw new SystemException();
            }   
        }

        public string printSelf()
        {
            if (isDirectory())
                 return "[" + fileInfo.FileName + "] inside " + parentDirectory + " & contents=[" + String.Join(",", items) + "]";
            else
                return "[" + fileInfo.FileName + "] inside " + parentDirectory;

        }

        private string GetParentPath()
        {
            return parentDirectory;
        }

        public Boolean isDirectory()
        {
            return fileInfo.Attributes.HasFlag(FileAttributes.Directory);
        }

        public Boolean AddNewInode(string fileName)
        {
            if (isDirectory())
            {
                //Dont care if directory or file. we can optimize later.
                items.Add(fileName);
                return true;
            }
            else
            {
                throw new SystemException();
            }
        }

        public IList<string> ListFilesWithPattern(string pattern)
        {
            if (pattern == "*")
                return items;
            else
            {
                IList<string> si = new List<string>();
                foreach (string item in items)
                {
                    if (item.IndexOf(pattern) == 0)
                        si.Add(item);
                }
                return si;
            }
        }

        public Boolean SetEndOfFile(long length)
        {
            if (isDirectory())
            {
                throw new NotSupportedException();
            }
            else
            {
                fileInfo.Length = length;
                return true;
            }
        }

        public void SetAttributes(FileAttributes newAttr)
        {

        }

        public Boolean RemoveInodeNameFromDirectory(string fileName)
        {
            foreach(string f in items)
            {
                if (f == fileName)
                {
                    items.Remove(fileName);
                    return true;
                }
            }
            return false;
        }

        private void ComputeBoundaries(int blocksize, long offset, int size, out int startfbn, out int startoffset, out int endfbn, out int endoffset)
        {
            startfbn = (int)(offset / blocksize);
            endfbn = (int)((offset + size) / blocksize);

            startoffset = (int)(offset % blocksize);
            endoffset = (int)((offset + size) % blocksize);
        }

        public Boolean WriteFile(byte[] buffer, out int bytesWritten, long offset)
        {
            int startfbn, startoffset, endfbn, endoffset;
            ComputeBoundaries(BLK_SIZE, offset, buffer.Length, out startfbn, out startoffset, out endfbn, out endoffset);

            int currentBufferOffset = 0;

            for (int fbn = startfbn; fbn <= endfbn; fbn++)
            {
                if (fbn == startfbn)
                {
                    if (!inCoreData.Contains(startfbn))
                    {
                        inCoreData.Add(startfbn, new byte[BLK_SIZE]);
                    }

                    byte[] data = (byte[])inCoreData[startfbn];
                    int tocopy = ((BLK_SIZE - startoffset) < buffer.Length) ? (BLK_SIZE - startoffset) : buffer.Length;
                    //Console.WriteLine("[currOffsetInInput, tocopy -> fbn, offsetInFBN ]" + currentBufferOffset + " copy -> " + startfbn + " , " + startoffset);
                    for (int k=0;k<tocopy; k++)
                    {
                        data[k + startoffset] = buffer[k + currentBufferOffset];
                    }
                    currentBufferOffset += tocopy;
                }
                else if (fbn == endfbn)
                {
                    if (!inCoreData.Contains(endfbn))
                    {
                        inCoreData.Add(endfbn, new byte[BLK_SIZE]);
                    }

                    byte[] data = (byte[])inCoreData[endfbn];
                    int tocopy = endoffset;
                    //Console.WriteLine("[currOffsetInInput, tocopy -> fbn, offsetInFBN ]" + currentBufferOffset + " copy -> " + endoffset + " , " + 0);

                    for (int k = 0; k < tocopy; k++)
                    {
                        data[k] = buffer[k + currentBufferOffset];
                    }
                    currentBufferOffset += tocopy;
                }
                else
                {
                    //proper BLK_SIZE size copy
                    if (!inCoreData.Contains(fbn))
                    {
                        inCoreData.Add(fbn, new byte[BLK_SIZE]);
                    }

                    byte[] data = (byte[])inCoreData[fbn];
                    //Console.WriteLine("[currOffsetInInput, tocopy -> fbn, offsetInFBN ]" + currentBufferOffset + " copy -> " + fbn + " , " +  0);

                    for (int k = 0; k < BLK_SIZE; k++)
                    {
                        data[k] = buffer[k + currentBufferOffset];
                    }
                    currentBufferOffset += BLK_SIZE;
                }
            }
            bytesWritten = currentBufferOffset;
            return true;
        }

        public Boolean ReadFile(byte[] buffer, out int bytesRead, long offset)
        {
            int startfbn, startoffset, endfbn, endoffset;
            ComputeBoundaries(BLK_SIZE, offset, buffer.Length, out startfbn, out startoffset, out endfbn, out endoffset);

            int currentBufferOffset = 0;

            for (int fbn = startfbn; fbn <= endfbn; fbn++)
            {
                if (fbn == startfbn)
                {
                    //return 0's if its not present incore
                    if (!inCoreData.Contains(startfbn))
                    {
                        inCoreData.Add(startfbn, new byte[BLK_SIZE]);
                    }

                    byte[] data = (byte[])inCoreData[startfbn];
                    int tocopy = ((BLK_SIZE - startoffset) < buffer.Length) ? (BLK_SIZE - startoffset) : buffer.Length;
                    //Console.WriteLine("[currOffsetInInput, tocopy -> fbn, offsetInFBN ]" + currentBufferOffset + " copy -> " + startfbn + " , " + startoffset);
                    for (int k = 0; k < tocopy; k++)
                    {
                        buffer[k + currentBufferOffset] = data[k + startoffset];
                    }
                    currentBufferOffset += tocopy;
                }
                else if (fbn == endfbn)
                {
                    if (!inCoreData.Contains(endfbn))
                    {
                        inCoreData.Add(endfbn, new byte[BLK_SIZE]);
                    }

                    byte[] data = (byte[])inCoreData[endfbn];
                    int tocopy = endoffset;
                    //Console.WriteLine("[currOffsetInInput, tocopy -> fbn, offsetInFBN ]" + currentBufferOffset + " copy -> " + endoffset + " , " + 0);

                    for (int k = 0; k < tocopy; k++)
                    {
                        buffer[k + currentBufferOffset] = data[k];
                    }
                    currentBufferOffset += tocopy;
                }
                else
                {
                    //proper 8k size copy
                    if (!inCoreData.Contains(fbn))
                    {
                        inCoreData.Add(fbn, new byte[BLK_SIZE]);
                    }

                    byte[] data = (byte[])inCoreData[fbn];
                    //Console.WriteLine("[currOffsetInInput, tocopy -> fbn, offsetInFBN ]" + currentBufferOffset + " copy -> " + fbn + " , " + 0);

                    for (int k = 0; k < BLK_SIZE; k++)
                    {
                        buffer[k + currentBufferOffset] = data[k];
                    }
                    currentBufferOffset += BLK_SIZE;
                }
            }
            bytesRead = currentBufferOffset;
            return true;
        }

        public Boolean FlushFileBuffers()
        {
            if (isDirty)
            {
                Console.WriteLine("Flush file buffers " + fileInfo.FileName);
            }
            return true;
        }

        /*
         * For a directory, remove all the file list and set the flag as skeleton
         * For a file, clear out all the buffers and set the flag. There should 
         * be no dirty/incode data after this call.
         */ 
        public Boolean MakeInodeAsSkeleton()
        {
            if (isDirectory())
            {
                //By now all files are  removed from the main 'inodes' dictionary
                //assert that directory is not dirty
            }
            else
            {
                //assert file does not have dirty buffers.
                //clear out all data in memory, this inode is removed from the 'inode' dictionary
            }
            isInodeSkeleton = true;
            return true;
        }
    }
}
