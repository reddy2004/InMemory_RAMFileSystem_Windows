using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DokanNet;
using Newtonsoft.Json;

namespace REDFS_ClusterMode
{
    class REDFSTree
    {
        IDictionary inodes = new Dictionary<string, REDFSInode>();

        public REDFSTree()
        {
            //Root of the file system
            inodes["\\"] = new REDFSInode(true, null, "\\");
        }

        public void LoadInodeMetaInfoFromDisk()
        {
            using (StreamReader sr = new StreamReader(@"C:\Users\vikra\Desktop\MY_GITHUB_UPLOADS\Data\inodes.json"))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    Console.WriteLine(line);
                    REDFSInode b2 = JsonConvert.DeserializeObject<REDFSInode>(line);
                    inodes.Add(b2.fileInfo.FileName, b2);
                }
            }
        }

        public void SaveInodeMetaInfoToDisk()
        {
            using (StreamWriter sw = new StreamWriter(@"C:\Users\vikra\Desktop\MY_GITHUB_UPLOADS\Data\inodes.json"))
            {
                foreach (DictionaryEntry kvp in inodes)
                {
                    REDFSInode content = (REDFSInode)kvp.Value;
                    String vstr = JsonConvert.SerializeObject(content, Formatting.None);
                    sw.WriteLine(vstr);
                }
            }
        }

        private Boolean isRootOfTree(string path)
        {
            return (path == "\\"); 
        }

        public void TestGetParentPath()
        {
            string fc;
            Console.WriteLine("\\   -> " + GetParentPath("\\", out fc) + " fc = " + fc);
            Console.WriteLine("\\file1.txt   -> " + GetParentPath("\\file1.txt", out fc) + " fc = " + fc);
            Console.WriteLine("\\dir1\\file1.txt   -> " + GetParentPath("\\dir1\\file1.txt", out fc) + " fc = " + fc);
        }

        private string GetParentPath(string fullPath, out string finalComponent)
        {
            if (fullPath == "\\")
            {
                finalComponent = "";
                return null;
            }
            else
            {
                string[] components = fullPath.Split("\\");
                //string.Join(",", components);
                string[] parent = new string[components.Length - 2];
                for (var i = 1; i < components.Length-1; i++)
                {
                    parent[i - 1] = components[i];
                }
                if (parent.Length == 0)
                {
                    finalComponent = components[1];
                    return "\\";
                }
                else
                {
                    finalComponent = components[components.Length - 1];
                    return "\\" + string.Join("\\", parent);
                }
            }
        }

        /*
         * Load the directory does the following,
         * IF directory in not incore - read it from the filesystem and populate the dictionary.
         * Load all the file name and meta info of this directory and populate the dictionlary
         */ 
        private Boolean LoadDirectory(string path)
        {
            if (inodes.Contains(path) || path == "\\")
            {
                //Load all the files/dir inside this directory in the dictionary and return
                //XX It could be the case that directory is present but not its children.
                return true;
            }
            else
            {
                string firstComponent; 
                //Now this directory in not incore. Lets load the parent first.
                if (LoadDirectory(GetParentPath(path, out firstComponent))) {
                    //We dont know if this directory is present at all in the parent path
                    return inodes.Contains(path);
                }
                else
                {
                    return false;
                }
            }
        }

        private REDFSInode GetInode(string fullPath)
        {
            if (inodes.Contains(fullPath))
            {
                return (REDFSInode)inodes[fullPath];
            } 
            else
            {
                string firstComponent;
                //Load directory should suceed provided that its in the filesystem.
                if (LoadDirectory(GetParentPath(fullPath, out firstComponent))) {
                    return GetInode(fullPath);
                }
                else
                {
                    //Parent directory does not exist in the system, so unlikely that this file exists
                    return null;
                }
            }
        }

        private Boolean CreateFileInternal(string inFolder, string fileName)
        {
            REDFSInode newFile = new REDFSInode(false, inFolder, fileName);
            REDFSInode directory = (REDFSInode)inodes[inFolder];
            
            if (inFolder == "\\")
            {
                inodes.Add("\\" + fileName, newFile);
            }
            else
            {
                inodes.Add(inFolder + "\\" + fileName, newFile);
            }
            if (!directory.items.Contains(fileName))
            {
                directory.AddNewInode(fileName);
            }
            return true;
        }

        private Boolean CreateDirectoryInternal(string inFolder, string dirName)
        {
            REDFSInode newDir = new REDFSInode(true, inFolder, dirName);
            REDFSInode directory = (REDFSInode)inodes[inFolder];
            directory.AddNewInode(dirName);

            //dont have additional slashes
            if (inFolder == "\\") inFolder = "";

            inodes.Add(inFolder + "\\" + dirName, newDir);
            return true;
        }

        public Boolean CreateFile(string filePath)
        {
            string firstComponent;
            string inDir = GetParentPath(filePath, out firstComponent);
            Console.WriteLine("CreateFile: " + filePath + " Log: Attempting to create new file [" + firstComponent + "] in " + inDir);
            return CreateFile(inDir, firstComponent);
        }

        public Boolean MoveInode(string srcPath, string destPath, bool replace, bool isDirectory)
        {
            if (!replace && FileExists(destPath))
            {
                return false;
            } 
            else if (FileExists(srcPath) && !isDirectory)
            {
                //src file exists, so copy this over to the destFile
                string srcFileName;
                string destFileName;
                string srcParent = GetParentPath(srcPath, out srcFileName);
                string destParent = GetParentPath(destPath, out destFileName);

                if (((REDFSInode)inodes[srcParent]).RemoveInodeNameFromDirectory(srcFileName))
                {
                    //copy inode we want to move
                    REDFSInode srcInode = (REDFSInode)inodes[srcPath];

                    //edit the inodes parent directory and new name
                    srcInode.parentDirectory = destParent;
                    srcInode.fileInfo.FileName = destFileName;

                    //remove from our main inodes list
                    inodes.Remove(srcPath);

                    //Add this to the destination parent with the new name
                    ((REDFSInode)inodes[destParent]).items.Add(destFileName);

                    //Create a new entry in our main inodes list
                    inodes.Add(destPath, srcInode);
                    return true;
                }

                return false;
            }
            else if (!replace && DirectoryExists(destPath))
            {
                return false;
            }
            else if(DirectoryExists(destPath) && isDirectory)
            {
                //src file exists, so copy this over to the destFile
                string srcFileName;
                string destFileName;
                string srcParent = GetParentPath(srcPath, out srcFileName);
                string destParent = GetParentPath(destPath, out destFileName);

                if (((REDFSInode)inodes[srcParent]).RemoveInodeNameFromDirectory(srcFileName))
                {
                    //copy inode we want to move
                    REDFSInode srcInode = (REDFSInode)inodes[srcPath];

                    //edit the inodes parent directory and new name
                    srcInode.parentDirectory = destParent;
                    srcInode.fileInfo.FileName = destFileName;

                    //remove from our main inodes list
                    inodes.Remove(srcPath);

                    //Add this to the destination parent with the new name
                    ((REDFSInode)inodes[destParent]).items.Add(destFileName);

                    //Create a new entry in our main inodes list
                    inodes.Add(destPath, srcInode);

                    //XXXX We are not yet done, we have to update parent path for all its children
                    throw new NotImplementedException();
                    return true;
                }
                return false;
            }
            else
            {
                return false;
            }
        }

        public Boolean CreateFile(string inFolder, string fileName)
        {
            if (!inodes.Contains(inFolder))
            {
                    if (!LoadDirectory(inFolder)) {
                        //parent directory where we want to create file is not present
                        return false;
                    }
                    //Parent directory is present and loaded.
                    return CreateFileInternal(inFolder, fileName);
            }
            else
            {
                //parent folder is incore
                return CreateFileInternal(inFolder, fileName);
            }
        }
        public Boolean CreateDirectory(string dirPath)
        {
            string firstComponent;
            string inDir = GetParentPath(dirPath, out firstComponent);
            Console.WriteLine("Attempting to create new directory [" + firstComponent + "] in " + inDir);
            return CreateDirectory(inDir, firstComponent);
        }

        public Boolean CreateDirectory(string inFolder,string directoryName) 
        {
            if (!inodes.Contains(inFolder))
            {
                if (!LoadDirectory(inFolder))
                {
                    //parent directory where we want to create file is not present
                    return false;
                }
                //Parent directory is present and loaded.
                return CreateDirectoryInternal(inFolder, directoryName);
            }
            else
            {
                //parent folder exists incore
                return CreateDirectoryInternal(inFolder, directoryName);
            }
        }

        public Boolean DeleteFile(string path)
        {
            if (LoadInode(path))
            {
                string finalComponent;
                string parent = GetParentPath(path, out finalComponent);
                REDFSInode rfi = (REDFSInode)inodes[parent];
                rfi.items.Remove(finalComponent);
                inodes.Remove(path);
                return true;
            }
            else
            {
                return false;
            }
        }

        public Boolean DeleteDirectoryInCleanup(string path)
        {
            if (LoadInode(path))
            {
                return DeleteDirectory(path);
            }
            else
            {
                //Already deleted
                return true;
            }
        }

        public Boolean DeleteDirectory(string path)
        {
            if (LoadInode(path))
            {
                string finalComponent;
                string parent = GetParentPath(path, out finalComponent);
                REDFSInode rfi = (REDFSInode)inodes[parent];
                if (!rfi.isDirectory())
                {
                    throw new NotImplementedException();
                }

                REDFSInode rfit = (REDFSInode)inodes[path];
                List<string> clist = (List<string>)rfit.ListFilesWithPattern("*");

                //first copy the list
                List<string> children = new List<string>();
                foreach (var c in clist)
                {
                    children.Add(c);
                }

                foreach (var child in children)
                {
                    string childpath = path + "\\" + child;
                    if (DirectoryExists(childpath))
                    {
                        DeleteDirectory(childpath);
                    }
                    else if (FileExists(childpath))
                    {
                        DeleteFile(childpath);
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }

                rfi.items.Remove(finalComponent);
                inodes.Remove(path);
                return true;
            }
            else
            {
                return false;
            }
        }

        public Boolean SetAttributes(string path, FileAttributes newAttr)
        {
            if (LoadInode(path))
            {
                REDFSInode rfi = (REDFSInode)inodes[path];
                rfi.SetAttributes(newAttr);
                return true;
            }
            else
            {
                return false;
            }
        }

        public Boolean FlushFileBuffers(string filePath)
        {
            if (LoadInode(filePath))
            {
                REDFSInode rfi = (REDFSInode)inodes[filePath];
                rfi.FlushFileBuffers();
                return true;
            }
            else
            {
                return false;
            }
        }

        public FileInformation getFileInformationOfRootDir()
        {
            return ((REDFSInode)inodes["\\"]).fileInfo;
        }

        public FileInformation GetFileInformationStruct(string path)
        {
            //XX todo, load it first
            if (LoadInode(path))
            {
                return ((REDFSInode)inodes[path]).fileInfo;
            }
            throw new SystemException();
        }

        public Boolean SetAllocationSize(string fileName, long length, Boolean IsDirectory)
        {
            if (IsDirectory)
            {
                return true;
            }
            else
            {
                if (LoadInode(fileName))
                {
                    ((REDFSInode)inodes[fileName]).fileInfo.Length = length;
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public Boolean DirectoryExists(string dirPath)
        {
            if (inodes.Contains(dirPath)) {
                REDFSInode i = (REDFSInode)inodes[dirPath];
                return i.isDirectory();
            }
            return false;
        }

        public Boolean FileExists(string filePath)
        {
            if (inodes.Contains(filePath))
            {
                REDFSInode i = (REDFSInode)inodes[filePath];
                return (i.isDirectory() == false);
            }
            return false;
        }

        public IList<FileInformation> FindFilesWithPattern(string dirPath, string pattern)
        {
            //In which directory? First load the directory since we want the FI of its children too
            //LoadDirectory(?)
            Boolean isSearchInRootFolder = false;
            if (dirPath == "\\")
            {
                //We are looking at the root directory;
                isSearchInRootFolder = true;
            }

            REDFSInode rfi = (REDFSInode)inodes["\\"];

            if (isSearchInRootFolder)
            {
                IList<FileInformation> lfi = new List<FileInformation>();

                IList<string> names = rfi.ListFilesWithPattern(pattern);

                foreach (var str in names)
                {
                    lfi.Add (((REDFSInode)inodes["\\" + str]).fileInfo);
                }
                return lfi;
            }
            else
            {
                if(LoadInode(dirPath))
                {
                    IList<FileInformation> lfi = new List<FileInformation>();
                    REDFSInode targetDir = (REDFSInode)inodes[dirPath];
                    IList<string> names = targetDir.ListFilesWithPattern(pattern);
                    foreach (var str in names)
                    {
                        lfi.Add(((REDFSInode)inodes[dirPath + "\\" + str]).fileInfo);
                    }
                    return lfi;
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
        }

        public Boolean WriteFile(string fileName, byte[] buffer, out int bytesWritten, long offset)
        {
            if (LoadInode(fileName))
            {
                REDFSInode rfi = (REDFSInode)inodes[fileName];
                return rfi.WriteFile(buffer, out bytesWritten, offset);
            }
            else
            {
                bytesWritten = 0;
                return false;
            }
        }

        public Boolean ReadFile(string fileName, byte[] buffer, out int bytesRead, long offset)
        {
            if (LoadInode(fileName))
            {
                return ((REDFSInode)inodes[fileName]).ReadFile(buffer, out bytesRead, offset);
            }
            else
            {
                bytesRead = 0;
                return false;
            }
        }

        private Boolean LoadInode(string fullPath)
        {
            if (inodes[fullPath] == null)
            {
                return false;
            }
            else
            {
                return true;
            }
            
        }

        public Boolean SetEndOfFile(string fileName, long length)
        {
            if (LoadInode(fileName))
            {
                return ((REDFSInode)inodes[fileName]).SetEndOfFile(length);
            }
            return false;
        }

        public Boolean SetFileAttributes(string fileName, FileAttributes attributes)
        {
            return true;
        }

        public bool SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime, DateTime? lastWriteTime)
        {
            return true;
        }

        public void PrintContents() 
        {
            Console.WriteLine("Printing contents of REDFSTree.");
            foreach (DictionaryEntry kvp in inodes)
            {
                string content = ((REDFSInode)kvp.Value).printSelf();
                Console.WriteLine("Key = {0}, Value = {1}", kvp.Key, content);
            }
            Console.WriteLine("Printing contents of REDFSTree. [DONE]");
        }
    }
}
