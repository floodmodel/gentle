﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;


namespace gentle
{
    public  class cAscRasterReader : IDisposable 
    {
        bool disposed = false;
        // Instantiate a SafeHandle instance.
        SafeHandle handle = new SafeFileHandle(IntPtr.Zero, true);
        private string[] mLinesForHeader;
        private double mDataValueOri;
        private cAscRasterHeader mHeader = new cAscRasterHeader();
        private string mHeaderStringAll;
        private string[] mSeparator = { " ", "\t", "," };
        private double[,] mValuesFromTL;
        public cRasterExtent extent;
        private const int BigSizeThreshold = 200000000;//2억개 기준


        public cAscRasterReader(string FPN)
        {
            mLinesForHeader = new string[8];
            int r = 0;
            foreach (string line in File.ReadLines(FPN))
            {
                if (r > 7) { break; }//7번 읽는다.. 즉, 7줄을 읽고, 끝낸다. header는 최대 6줄이다.
                mLinesForHeader[r] = line;
                r++;
            }
           mHeader = GetHeaderInfo(mLinesForHeader, mSeparator);
            mHeaderStringAll = cTextFile.MakeHeaderString(mHeader.numberCols, mHeader.numberRows,
                            mHeader.xllcorner, mHeader.yllcorner, mHeader.cellsize, mHeader.nodataValue.ToString());
            extent = new cRasterExtent(mHeader);
            mValuesFromTL = new double[mHeader.numberCols, mHeader.numberRows ];
            int headerEndingIndex =mHeader .headerEndingLineIndex;
            bool isBigSize = false;
            if(mHeader.numberCols * mHeader.numberRows > BigSizeThreshold) { isBigSize = true; }
            if (isBigSize == false)
            {
                string[] allLines = File.ReadAllLines(FPN, Encoding.Default);
                var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
                Parallel.For(headerEndingIndex + 1, allLines.Length, options, delegate (int ly)
                  {
                      string[] values = allLines[ly].Split(mSeparator, StringSplitOptions.RemoveEmptyEntries);
                      int y = ly - headerEndingIndex - 1;
                      for (int x = 0; x < values.Length; x++)
                      {
                          double v = 0;
                          if (double.TryParse(values[x], out v) == true)
                          {
                              mValuesFromTL[x, y] = v;
                          }
                          else
                          {
                              mValuesFromTL[x, y] = Header.nodataValue;
                          }
                      }
                  });
            }
            else
            {
                int nl = 0;
                int y = 0;
                foreach (string line in File.ReadLines(FPN))
                {
                    if (nl > headerEndingIndex)
                    {
                        string[] values = line.Split(mSeparator, StringSplitOptions.RemoveEmptyEntries);
                        for (int x = 0; x < values.Length; x++)
                        {
                            double v = 0;
                            if (double.TryParse(values[x], out v) == true)
                            {
                                mValuesFromTL[x, y] = v;
                            }
                            else
                            {
                                mValuesFromTL[x, y] = Header.nodataValue;
                            }
                        }
                        y++;
                    }
                    nl++;
                }
            }           
        }


        public void Dispose()
        {
            Dispose(true);
            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                handle.Dispose();
                // Free any other managed objects here.
            }

            disposed = true;
        }

        public static cAscRasterHeader GetHeaderInfo(string inAscFPN)
        {
            string[] LinesForHeader = new string[8];
             string[] separator = { " ", "\t", "," };
             int r = 0;
            foreach (string line in File.ReadLines(inAscFPN))
            {
                if (r > 7) { break; }//7번 읽는다.. 즉, 7줄을 읽고, 끝낸다. heade는 최대 6줄이다.
                LinesForHeader[r] = line;
                r++;
            }
            return GetHeaderInfo(LinesForHeader, separator);
        }

        private static cAscRasterHeader GetHeaderInfo(string[] LinesForHeader, string[] separator)
        {
            cAscRasterHeader header = new cAscRasterHeader();
            header.dataStartingLineIndex = -1;
            for (int ln = 0; ln < LinesForHeader.Length; ln++)
            {
                string aline = LinesForHeader[ln];
                string[] LineParts = aline.Split(separator, StringSplitOptions.RemoveEmptyEntries);

                int iv = 0;
                double dv = 0;
                switch (ln)
                {
                    case 0:
                        if (int.TryParse(LineParts[1], out iv))
                        {
                            header.numberCols = iv;
                        }
                        else
                        {
                            header.numberCols = -1;
                        }
                        break;
                    case 1:
                        if (int.TryParse(LineParts[1], out iv))
                        {
                            header.numberRows = iv;
                        }
                        else
                        {
                            header.numberRows = -1;
                        }
                        break;
                    case 2:
                        if (double.TryParse(LineParts[1], out dv))
                        {
                            header.xllcorner = dv;
                        }
                        else
                        {
                            header.xllcorner = -1;
                        }
                        break;
                    case 3:
                        if (double.TryParse(LineParts[1], out dv))
                        {
                            header.yllcorner = dv;
                        }
                        else
                        {
                            header.yllcorner = -1;
                        }
                        break;
                    case 4:
                        if (double.TryParse(LineParts[1], out dv))
                        {
                            header.cellsize = dv;
                        }
                        else
                        {
                            header.cellsize = -1;
                        }
                        break;
                    case 5:
                        if (string.IsNullOrEmpty(LineParts[1]))
                        {
                            header.nodataValue = -9999;
                        }
                        else
                        {
                            if (double.TryParse(LineParts[1], out double v))
                            {
                                header.nodataValue = (int) v;
                            }
                            else
                            {
                                header.nodataValue = -1;
                            }
                        }
                        break;
                }

                float Val = 0;
                if (LineParts.Length > 0 && float.TryParse(LineParts[0], out Val) == true)
                {
                    header.dataStartingLineIndex= ln;
                    header.headerEndingLineIndex = ln - 1;
                    return header;
                }
            }
            return header;
        }

        public cAscRasterHeader Header
        {
            get
            {
                return mHeader;
            }
        }

        public string HeaderStringAll
        {
            get
            {
                return mHeaderStringAll;
            }
        }

        public double cellSize
        {
            get
            {
                return mHeader.cellsize ;
            }
        }

        public static bool CheckTwoGridLayerExtent( cAscRasterReader  GridBase, cAscRasterReader GridTarget)
        {
            if (GridBase.Header.numberCols  != GridTarget.Header.numberCols ){ return false; }
            if (GridBase.Header.numberRows  != GridTarget.Header.numberRows) { return false; }
            if (GridBase.extent.bottom != GridTarget.extent.bottom) { return false; }
            if (GridBase.extent .top != GridTarget.extent.top) { return false; }
            if (GridBase.extent.left != GridTarget.extent.left) { return false; }
            if (GridBase.extent.right != GridTarget.extent.right) { return false; }
                return true;
        }

        public static bool CheckTwoGridLayerExtentUsingRowAndColNum(cAscRasterReader GridBase, cAscRasterReader GridTarget)
        {
            if (GridBase.Header.numberCols != GridTarget.Header.numberCols) { return false; }
            if (GridBase.Header.numberRows != GridTarget.Header.numberRows) { return false; }
            return true;
        }

        public static CellPosition[] GetPositiveCellsPositions(cAscRasterReader inGrid)
        {
            List<CellPosition> cells = new List<CellPosition>();
            for (int y = 0; y < inGrid.Header.numberRows; y++)
            {
                for (int x = 0; x < inGrid.Header.numberCols; x++)
                {
                    if (inGrid.ValueFromTL(x, y) > 0)
                    {
                        CellPosition cp;
                        cp.x = x;
                        cp.y = y;
                        cells.Add(cp);
                    }
                }
            }
            return cells.ToArray();
        }


        public static double CellsAverageValue(CellPosition[] targetCells, cAscRasterReader inASC, bool allowNegative)
        {
            double sum = 0;
            for (int n = 0; n < targetCells.Length; n++)
            {
                double v = inASC .mValuesFromTL [targetCells[n].x, targetCells[n].y];
                if (allowNegative == false && v < 0)
                {
                    v = 0;
                }
                sum += v;
            }
            return sum / targetCells.Length;
        }


        public static void MakeNewAsciiRasterFile(cAscRasterReader baseGrid,
                                             string fpn, cData.DataType dType, double defaultValue)
        {
            Console.WriteLine("This was not developed yet.");
        }


        /// <summary>
        /// Column and row numbers are started from zero
        /// </summary>
        /// <param name="xColNumber"></param>
        /// <param name="yRowNumber"></param>
        /// <returns></returns>
        public double ValueFromLL(int xColNumber, int yRowNumber)
        {
            int row = mHeader .numberRows  - yRowNumber - 1;
            return mValuesFromTL[xColNumber, row];
        }

        /// <summary>
        /// Column and row numbers are started from zero
        /// </summary>
        /// <param name="xColNumber"></param>
        /// <param name="yRowNumber"></param>
        /// <returns></returns>
        public double ValueFromTL(int xColNumber, int yRowNumber)
        {
            return mValuesFromTL[xColNumber, yRowNumber];
        }


        /// <summary>
        /// Column and row numbers are started from zero
        /// </summary>
        /// <param name="xColNumber"></param>
        /// <param name="yRowNumber"></param>
        /// <returns></returns>
        public double[,] ValuesFromTL()
        {
            return mValuesFromTL;
        }

        /// <summary>
        /// Column and row numbers are started from zero
        /// </summary>
        /// <param name="xColNumber"></param>
        /// <param name="yRowNumber"></param>
        /// <returns></returns>
        public double ValueFromTLasNotNegative(int xColNumber, int yRowNumber)
        {
            mDataValueOri = mValuesFromTL[xColNumber, yRowNumber];
            if (mDataValueOri < 0)
            {
                return 0;
            }
            else
            {
                return mDataValueOri;
            }
        }


        /// <summary>
        /// Column and row numbers are started from zero
        /// </summary>
        /// <param name="xcol"></param>
        /// <param name="yrow"></param>
        /// <returns></returns>
        public double ValueFromLLasNotNegative(int xcol, int yrow)
        {
            mDataValueOri = ValueFromLL(xcol, yrow);
            if (mDataValueOri < 0)
            {
                return 0;
            }
            else
            {
                return mDataValueOri;
            }
        }


        public int DataStartLineInASCfile
        {
            get
            {
                return mHeader .dataStartingLineIndex;
            }
        }


        public string OneRowContainsValuesFromTop(int yrowIndex)
        {
            StringBuilder sb = new StringBuilder();
            for (int x = 0; x < mHeader.numberCols; x++)
            {
                sb.Append(mValuesFromTL[x, yrowIndex] + " ");
            }
            return sb.ToString();
        }

        public static bool CompareFiles(cAscRasterReader FileToReference, cAscRasterReader FileToCompare)
        {
            if (FileToReference.Header.numberCols != FileToCompare.Header.numberCols)
                return false;
            if (FileToReference.Header.numberRows != FileToCompare.Header.numberRows)
                return false;
            if (FileToReference.Header.cellsize != FileToCompare.Header.cellsize)
                return false;
            return true;
        }
    }
}
