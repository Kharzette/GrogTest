using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UtilityLib;
using MeshLib;
using PathLib;

using SharpDX;
using SharpDX.DXGI;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;

using MatLib	=MaterialLib.MaterialLib;
using Buffer	=SharpDX.Direct3D11.Buffer;
using Device	=SharpDX.Direct3D11.Device;


namespace TestPathing
{
	internal class DrawPathing
	{
		Buffer	mVBNodes, mIBNodes;
		Buffer	mVBCons, mIBCons;
		Buffer	mVBPath, mIBPath;

		VertexBufferBinding	mVBBNodes, mVBBCons, mVBBPath;

		int	mNodeIndexCount;
		int	mConIndexCount;
		int	mPathIndexCount;

		bool	mbDrawNodeFaces		=true;
		bool	mbDrawConnections	=true;

		Vector3	mLightDir;
		Random	mRand	=new Random();

		GraphicsDevice	mGD;

		MatLib	mMatLib;


		internal DrawPathing(GraphicsDevice gd, MaterialLib.StuffKeeper sk)
		{
			mGD		=gd;
			mMatLib	=new MatLib(gd, sk);

			mLightDir	=Mathery.RandomDirection(mRand);

			Vector4	lightColor2	=Vector4.One * 0.8f;
			Vector4	lightColor3	=Vector4.One * 0.6f;

			lightColor2.W	=lightColor3.W	=1f;

			mMatLib.CreateMaterial("LevelGeometry");
			mMatLib.SetMaterialEffect("LevelGeometry", "Static.fx");
			mMatLib.SetMaterialTechnique("LevelGeometry", "TriVColorSolidSpec");
			mMatLib.SetMaterialParameter("LevelGeometry", "mLightColor0", Vector4.One);
			mMatLib.SetMaterialParameter("LevelGeometry", "mLightColor1", lightColor2);
			mMatLib.SetMaterialParameter("LevelGeometry", "mLightColor2", lightColor3);
			mMatLib.SetMaterialParameter("LevelGeometry", "mSolidColour", Vector4.One);
			mMatLib.SetMaterialParameter("LevelGeometry", "mSpecPower", 1);
			mMatLib.SetMaterialParameter("LevelGeometry", "mSpecColor", Vector4.One);
			mMatLib.SetMaterialParameter("LevelGeometry", "mWorld", Matrix.Identity);
		}


		internal void BuildDrawInfo(PathGraph graph)
		{
			FreeVBs();

			List<Vector3>	verts		=new List<Vector3>();
			List<Vector3>	norms		=new List<Vector3>();
			List<UInt32>	indexes		=new List<UInt32>();
			List<int>		vertCounts	=new List<int>();
			
			graph.GetNodePolys(verts, indexes, norms, vertCounts);

			if(verts.Count == 0)
			{
				return;
			}

			VPosNormCol0	[]nodeVerts	=new VPosNormCol0[verts.Count];
			for(int i=0;i < nodeVerts.Length;i++)
			{
				nodeVerts[i].Position	=verts[i] + Vector3.UnitY;	//boost up 1
				nodeVerts[i].Normal.X	=norms[i].X;
				nodeVerts[i].Normal.Y	=norms[i].Y;
				nodeVerts[i].Normal.Z	=norms[i].Z;
				nodeVerts[i].Normal.W	=1f;
			}

			int	idx	=0;
			for(int i=0;i < vertCounts.Count;i++)
			{
				Color	col	=Mathery.RandomColor(mRand);

				for(int j=0;j < vertCounts[i];j++)
				{
					nodeVerts[idx + j].Color0	=col;
				}
				idx	+=vertCounts[i];
			}

			mVBNodes	=VertexTypes.BuildABuffer(mGD.GD, nodeVerts, VertexTypes.GetIndex(nodeVerts[0].GetType()));
			mIBNodes	=VertexTypes.BuildAnIndexBuffer(mGD.GD, indexes.ToArray());
			mVBBNodes	=VertexTypes.BuildAVBB(VertexTypes.GetIndex(nodeVerts[0].GetType()), mVBNodes);

			mNodeIndexCount	=indexes.Count;

			//connexions
			List<PathGraph.LineSeg>	segz	=graph.GetNodeConnections();

			if(segz.Count <= 0)
			{
				return;
			}
			VPosNormCol0	[]segVerts	=new VPosNormCol0[segz.Count * 3];

			UInt32	index	=0;
			indexes.Clear();
			foreach(PathGraph.LineSeg seg in segz)
			{
				Color	col	=Mathery.RandomColor(mRand);

				//endpoint
				segVerts[index].Position	=seg.mB;

				Vector3	lineVec	=seg.mB - seg.mA;

				//get a perpindicular axis to the a to b axis
				//so the back side of the connection can flare out a bit
				Vector3	crossVec	=Vector3.Cross(lineVec, Vector3.UnitY);

				crossVec.Normalize();

				Vector3	normVec	=Vector3.Cross(crossVec, lineVec);

				normVec.Normalize();

				crossVec	*=2f;

				segVerts[index + 1].Position	=seg.mA - crossVec + Mathery.RandomDirectionXZ(mRand);
				segVerts[index + 2].Position	=seg.mA + crossVec + Mathery.RandomDirectionXZ(mRand);

				segVerts[index].Color0		=col;
				segVerts[index + 1].Color0	=col;
				segVerts[index + 2].Color0	=col;

				//adjust up
				segVerts[index].Position		+=Vector3.UnitY * 2f;
				segVerts[index + 1].Position	+=Vector3.UnitY * 1.7f;
				segVerts[index + 2].Position	+=Vector3.UnitY * 1.7f;

				Half4	norm;
				norm.X	=normVec.X;
				norm.Y	=normVec.Y;
				norm.Z	=normVec.Z;
				norm.W	=1f;
				segVerts[index].Normal		=norm;
				segVerts[index + 1].Normal	=norm;
				segVerts[index + 2].Normal	=norm;

				indexes.Add(index);
				indexes.Add(index + 1);
				indexes.Add(index + 2);

				index	+=3;
			}

			mVBCons		=VertexTypes.BuildABuffer(mGD.GD, segVerts, VertexTypes.GetIndex(segVerts[0].GetType()));
			mIBCons		=VertexTypes.BuildAnIndexBuffer(mGD.GD, indexes.ToArray());
			mVBBCons	=VertexTypes.BuildAVBB(VertexTypes.GetIndex(segVerts[0].GetType()), mVBCons);

			mConIndexCount	=indexes.Count;
		}


		internal void BuildPathDrawInfo(List<Vector3> path, Vector3 boxMiddleOffset)
		{
			if(mVBPath != null)
			{
				mVBPath.Dispose();
			}
			if(mIBPath != null)
			{
				mIBPath.Dispose();
			}

			if(path.Count < 2)
			{
				return;
			}

			VPosNormCol0	[]segVerts	=new VPosNormCol0[(path.Count - 1) * 3];

			UInt32			index		=0;
			List<UInt32>	indexes		=new List<UInt32>();
			for(int i=0;i < (path.Count - 1);i++)
			{
				Color	col	=Mathery.RandomColor(mRand);

				col	=Color.Red;

				//endpoint
				segVerts[index].Position	=path[i + 1];

				Vector3	lineVec	=path[i + 1] - path[i];

				//get a perpindicular axis to the a to b axis
				//so the back side of the connection can flare out a bit
				Vector3	crossVec	=Vector3.Cross(lineVec, Vector3.UnitY);

				crossVec.Normalize();

				Vector3	normVec	=Vector3.Cross(crossVec, lineVec);

				normVec.Normalize();

				crossVec	*=2f;

				segVerts[index + 1].Position	=path[i] - crossVec + Mathery.RandomDirectionXZ(mRand);
				segVerts[index + 2].Position	=path[i] + crossVec + Mathery.RandomDirectionXZ(mRand);

				segVerts[index].Color0		=col;
				segVerts[index + 1].Color0	=col;
				segVerts[index + 2].Color0	=col;

				//adjust up
				segVerts[index].Position		+=boxMiddleOffset;
				segVerts[index + 1].Position	+=boxMiddleOffset;
				segVerts[index + 2].Position	+=boxMiddleOffset;

				Half4	norm;
				norm.X	=normVec.X;
				norm.Y	=normVec.Y;
				norm.Z	=normVec.Z;
				norm.W	=1f;
				segVerts[index].Normal		=norm;
				segVerts[index + 1].Normal	=norm;
				segVerts[index + 2].Normal	=norm;

				indexes.Add(index);
				indexes.Add(index + 1);
				indexes.Add(index + 2);

				index	+=3;
			}

			mVBPath		=VertexTypes.BuildABuffer(mGD.GD, segVerts, VertexTypes.GetIndex(segVerts[0].GetType()));
			mIBPath		=VertexTypes.BuildAnIndexBuffer(mGD.GD, indexes.ToArray());
			mVBBPath	=VertexTypes.BuildAVBB(VertexTypes.GetIndex(segVerts[0].GetType()), mVBPath);

			mPathIndexCount	=indexes.Count;
		}


		internal void FreeAll()
		{
			mMatLib.FreeAll();

			FreeVBs();
		}


		internal void FreeVBs()
		{
			if(mVBNodes != null)
			{
				mVBNodes.Dispose();
			}
			if(mIBNodes != null)
			{
				mIBNodes.Dispose();
			}
			if(mVBCons != null)
			{
				mVBCons.Dispose();
			}
			if(mIBCons != null)
			{
				mIBCons.Dispose();
			}
			if(mVBPath != null)
			{
				mVBPath.Dispose();
			}
			if(mIBPath != null)
			{
				mIBPath.Dispose();
			}
		}


		internal void Draw()
		{
			if(mVBNodes == null)
			{
				return;
			}

			if(!mbDrawNodeFaces && !mbDrawConnections && mVBPath == null)
			{
				return;
			}

			mMatLib.UpdateWVP(Matrix.Identity, mGD.GCam.View, mGD.GCam.Projection, mGD.GCam.Position);

			mMatLib.ApplyMaterialPass("LevelGeometry", mGD.DC, 0);

			//node faces
			if(mbDrawNodeFaces)
			{
				mGD.DC.InputAssembler.SetVertexBuffers(0, mVBBNodes);
				mGD.DC.InputAssembler.SetIndexBuffer(mIBNodes, Format.R32_UInt, 0);
				mGD.DC.DrawIndexed(mNodeIndexCount, 0, 0);
			}

			//node connections
			if(mbDrawConnections)
			{
				mGD.DC.InputAssembler.SetVertexBuffers(0, mVBBCons);
				mGD.DC.InputAssembler.SetIndexBuffer(mIBCons, Format.R32_UInt, 0);
				mGD.DC.DrawIndexed(mConIndexCount, 0, 0);
			}

			if(mVBPath != null)
			{
				mGD.DC.InputAssembler.SetVertexBuffers(0, mVBBPath);
				mGD.DC.InputAssembler.SetIndexBuffer(mIBPath, Format.R32_UInt, 0);
				mGD.DC.DrawIndexed(mPathIndexCount, 0, 0);
			}
		}


		internal void DrawSettings(int stuff)
		{
			mbDrawNodeFaces		=((stuff & 1) != 0)? true : false;
			mbDrawConnections	=((stuff & 2) != 0)? true : false;
		}
	}
}
