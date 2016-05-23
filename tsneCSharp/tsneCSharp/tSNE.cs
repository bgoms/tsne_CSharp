using System;
using System.Collections.Generic;

namespace tsneCSharp
{
    /// tSNECSharp
    /// 
    /// Implementation of t-SNE in C#. The implementation was tested on Microsoft Visual Studio 2015.
    /// Converted from JavaScript implementation - https://github.com/karpathy/tsnejs
    /// 
    /// Written by Youngbin Pyo on 05-23-16
    public class tSNE
    {
        #region ###### Variables ######
        private double mPerplexity; // effective number of nearest neighbors
        private int mDim; // dimensionality of the embedding
        private double mEpsilon; // learning rate
        private double mIter;

        private bool mRet = false;
        private double mVal = 0.0;

        private double[] mP;
        private double[][] mY; // Y is an array of 2-D points that you can plot
        private double[][] mGains;
        private double[][] mYStep;
        private int mN;

        private double mCost;
        private double[][] mGrad;
        #endregion

        #region ###### Constructor ######
        public tSNE(double perplexity, int dim, double epsilon)
        {
            this.mPerplexity = perplexity;
            this.mDim = dim;
            this.mEpsilon = epsilon;

            this.mIter = 0;
        }
        #endregion

        #region ###### Public Methods ######
        // return current solution
        public double[][] GetSolution()
        {
            return this.mY;
        }

        // this function takes a set of high-dimensional points
        // and creates matrix P from them using gaussian kernel
        public void InitDataRaw(double[][] X)
        {
            int N = X.Length;
            int D = X[0].Length;

            double[] dists = this.xtod(X); // convert X to distances using gaussian kernel
            this.mP = d2p(dists, this.mPerplexity, 1e-4); // attach to object
            this.mN = N; // back up the size of the dataset
            this.InitSolution(); // refresh this
        }

        // this function takes a given distance matrix and creates
        // matrix P from them.
        // D is assumed to be provided as a list of lists, and should be symmetric
        public void InitDataDist(double[][] D)
        {
            int N = D.Length;
            // convert D to a (fast) typed array version
            double[] dists = zeros(N * N); // allocate contiguous array

            for (int i = 0; i < N; i++)
            {
                for (int j = i + 1; j < N; j++)
                {
                    double d = D[i][j];
                    dists[i * N + j] = d;
                    dists[j * N + i] = d;
                }
            }

            this.mP = d2p(dists, this.mPerplexity, 1e-4);
            this.mN = N;
            this.InitSolution(); // refresh this
        }

        // (re)initializes the solution to random
        private void InitSolution()
        {
            // generate random solution to t-SNE
            this.mY = randn2d(this.mN, this.mDim, double.PositiveInfinity); // the solution
            this.mGains = randn2d(this.mN, this.mDim, 1.0); // step gains to accelerate progress in unchanging directions
            this.mYStep = randn2d(this.mN, this.mDim, 0.0); // momentum accumulator
            this.mIter = 0;
        }

        // perform a single step of optimization to improve the embedding
        public double Step()
        {
            this.mIter += 1;
            int N = this.mN;

            this.CostGrad(this.mY); // evaluate gradient
            double cost = this.mCost;
            double[][] grad = this.mGrad;

            // perform gradient step
            double[] ymean = zeros(this.mDim);
            for (int i = 0; i < N; i++)
            {
                for (int d = 0; d < this.mDim; d++)
                {
                    double gid = grad[i][d];
                    double sid = this.mYStep[i][d];
                    double gainid = this.mGains[i][d];

                    // compute gain update
                    double newgain = Sign(gid) == Sign(sid) ? gainid * 0.8 : gainid + 0.2;
                    if (newgain < 0.01) newgain = 0.01; // clamp
                    this.mGains[i][d] = newgain; // store for next turn

                    // compute momentum step direction
                    var momval = this.mIter < 250 ? 0.5 : 0.8;
                    var newsid = momval * sid - this.mEpsilon * newgain * grad[i][d];
                    this.mYStep[i][d] = newsid; // remember the step we took

                    // step!
                    this.mY[i][d] += newsid;

                    ymean[d] += this.mY[i][d]; // accumulate mean so that we can center later
                }
            }

            // reproject Y to be zero mean
            for (int i = 0; i < N; i++)
            {
                for (int d = 0; d < this.mDim; d++)
                {
                    this.mY[i][d] -= ymean[d] / N;
                }
            }

            //if(this.iter%100===0) console.log('iter ' + this.iter + ', cost: ' + cost);
            return cost; // return current cost
        }
        #endregion

        #region ###### Core Methods ######
        // return cost and gradient, given an arrangement
        private void CostGrad(double[][] Y)
        {
            int N = this.mN;
            int dim = this.mDim; // dim of output space
            double[] P = this.mP;

            int pmul = this.mIter < 100 ? 4 : 1; // trick that helps with local optima

            // compute current Q distribution, unnormalized first
            double[] Qu = zeros(N * N);
            double qsum = 0.0;

            for (int i = 0; i < N; i++)
            {
                for (int j = i + 1; j < N; j++)
                {
                    double dsum = 0.0;
                    for (int d = 0; d < dim; d++)
                    {
                        double dhere = Y[i][d] - Y[j][d];
                        dsum += dhere * dhere;
                    }

                    double qu = 1.0 / (1.0 + dsum); // Student t-distribution
                    Qu[i * N + j] = qu;
                    Qu[j * N + i] = qu;
                    qsum += 2 * qu;
                }
            }

            // normalize Q distribution to sum to 1
            int NN = N * N;
            double[] Q = zeros(NN);

            for (int q = 0; q < NN; q++)
            {
                Q[q] = Math.Max(Qu[q] / qsum, 1e-100);
            }

            double cost = 0.0;
            List<double[]> grad = new List<double[]>();

            for (int i = 0; i < N; i++)
            {
                double[] gsum = new double[dim]; // init grad for point i
                for (int d = 0; d < dim; d++) gsum[d] = 0.0;

                for (int j = 0; j < N; j++)
                {
                    cost += -P[i * N + j] * Math.Log(Q[i * N + j]); // accumulate cost (the non-constant portion at least...)
                    double premult = 4 * (pmul * P[i * N + j] - Q[i * N + j]) * Qu[i * N + j];
                    for (int d = 0; d < dim; d++)
                    {
                        gsum[d] += premult * (Y[i][d] - Y[j][d]);
                    }
                }

                grad.Add(gsum);
            }

            this.mCost = cost;
            this.mGrad = new double[grad.Count][];
            for (int i = 0; i < grad.Count; i++)
            {
                this.mGrad[i] = grad[i];
            }
        }

        // return 0 mean unit standard deviation random number
        private double GaussRandom()
        {
            if (mRet)
            {
                mRet = false;
                return mVal;
            }

            System.Threading.Thread.Sleep(5);
            Random rnd = new Random();
            double u = rnd.NextDouble() - 1;
            double v = rnd.NextDouble() - 1;
            double r = u * u + v * v;

            if (r == 0 || r > 1)
            {
                return GaussRandom();
            }

            double c = Math.Sqrt(-2 * Math.Log(r) / r);
            mVal = v * c; // cache this for next function call for efficiency
            mRet = true;
            return u * c;
        }

        // return random normal number
        private double randn(double mu, double std)
        {
            return mu + GaussRandom() * std;
        }

        // utilitity that creates contiguous vector of zeros of size n
        private double[] zeros(int n)
        {
            double[] arr = new double[n];

            for (int i = 0; i < n; i++) arr[i] = 0;

            return arr;
        }

        // utility that returns 2d array filled with random numbers
        // or with value s, if provided
        private double[][] randn2d(int n, int d, double s)
        {
            bool uses = (s != double.PositiveInfinity);
            List<double[]> x = new List<double[]>();

            for (int i = 0; i < n; i++)
            {
                List<double> xhere = new List<double>();
                for (int j = 0; j < d; j++)
                {
                    if (uses)
                    {
                        xhere.Add(s);   
                    }
                    else
                    {
                        xhere.Add(randn(0.0, 1e-4));
                    }                    
                }

                x.Add(xhere.ToArray());
            }

            double[][] ret = new double[x.Count][];
            for (int i = 0; i < x.Count; i++)
            {
                ret[i] = x[i];
            }

            return ret;
        }

        // compute L2 distance between two vectors
        private double L2(double[] x1, double[] x2)
        {
            int N = x1.Length;
            double d = 0;

            for (int i = 0; i < N; i++)
            {
                double x1i = x1[i];
                double x2i = x2[i];

                d += (x1i - x2i) * (x1i - x2i);
            }

            return d;
        }

        // compute pairwise distance in all vectors in X
        private double[] xtod(double[][] X)
        {
            int N = X.Length;
            double[] dist = zeros(N * N); // allocate contiguous array

            for (int i = 0; i < N; i++)
            {
                for (int j = i + 1; j < N; j++)
                {
                    double d = L2(X[i], X[j]);
                    dist[i * N + j] = d;
                    dist[j * N + i] = d;
                }
            }

            return dist;
        }

        // compute (p_{i|j} + p_{j|i})/(2n)
        private double[] d2p(double[] D, double perplexity, double tol)
        {
            double Nf = Math.Sqrt(D.Length); // this better be an integer
            int N = (int)Math.Floor(Nf);

            double Htarget = Math.Log(perplexity); // target entropy of distribution
            double[] P = zeros(N * N); // temporary probability matrix

            double[] prow = zeros(N); // a temporary storage compartment

            for (int i = 0; i < N; i++)
            {
                double betamin = double.NegativeInfinity;
                double betamax = double.PositiveInfinity;
                double beta = 1; // initial value of precision
                bool done = false;
                int maxtries = 50;

                // perform binary search to find a suitable precision beta
                // so that the entropy of the distribution is appropriate
                int num = 0;
                while (!done)
                {
                    //debugger;

                    // compute entropy and kernel row with beta precision
                    double psum = 0.0;
                    for (int j = 0; j < N; j++)
                    {
                        double pj = Math.Exp(-D[i * N + j] * beta);
                        if (i == j) pj = 0; // we dont care about diagonals
                        prow[j] = pj;
                        psum += pj;
                    }

                    // normalize p and compute entropy
                    double Hhere = 0.0;
                    for (int j = 0; j < N; j++)
                    {
                        double pj = prow[j] / psum;
                        prow[j] = pj;

                        if (pj > 1e-7) Hhere -= pj * Math.Log(pj);
                    }

                    // adjust beta based on result
                    if (Hhere > Htarget)
                    {
                        // entropy was too high (distribution too diffuse)
                        // so we need to increase the precision for more peaky distribution
                        betamin = beta; // move up the bounds
                        if (betamax == Double.PositiveInfinity) beta = beta * 2;
                        else beta = (beta + betamax) / 2;
                    }
                    else
                    {
                        // converse case. make distrubtion less peaky
                        betamax = beta;
                        if (betamin == Double.NegativeInfinity) beta = beta / 2;
                        else beta = (beta + betamin) / 2;
                    }

                    // stopping conditions: too many tries or got a good precision
                    num++;
                    if (Math.Abs(Hhere - Htarget) < tol) done = true;
                    if (num >= maxtries) done = true;
                }

                // console.log('data point ' + i + ' gets precision ' + beta + ' after ' + num + ' binary search steps.');
                // copy over the final prow to P at row i
                for (int j = 0; j < N; j++)
                {
                    P[i * N + j] = prow[j];
                }
            } // end loop over examples i

            // symmetrize P and normalize it to sum to 1 over all ij
            double[] Pout = zeros(N * N);
            int N2 = N * 2;

            for (int i = 0; i < N; i++)
            {
                for (int j = 0; j < N; j++)
                {
                    Pout[i * N + j] = Math.Max((P[i * N + j] + P[j * N + i]) / N2, 1e-100);
                }
            }

            return Pout;
        }

        // helper function
        private double Sign(double x)
        {
            return x > 0 ? 1 : x < 0 ? -1 : 0;
        }

        #endregion
    }
}
