﻿using UnityEngine;
using System.Collections;
using Emgu.CV;
using Emgu.CV.Util;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using System.Drawing;

[RequireComponent(typeof(Camera))]
public class Mappity : MonoBehaviour {

	//CV variables
	public int numPoints = 6;
	
	Vector3[][] unityObjectPoints;
	Vector2[][] unityImagePoints;

	MCvPoint3D32f[][] objectPoints;
	PointF[][] imagePoints;

	Size imageSize;
	IntrinsicCameraParameters intrinsics;
	CALIB_TYPE calibrationType;
	MCvTermCriteria termCriteria;
	ExtrinsicCameraParameters[] extrinsics;
	
	Texture errorTex;
	public double calibrationError;
	
	//CV <> Unity conversion
	Matrix4x4 cvExtrinsics;
	Matrix4x4 cvIntrinsics;
	
	
	//control
	public bool autoCalibrate;
	
	//ui
	public Texture targetTex;
	public Texture overTex;
	
	public int selectedTargetIndex = -1;
	
	
	//testing
	public GameObject calibObject;
	
	// Use this for initialization
	void Start () {

		//init variables
		unityObjectPoints = new Vector3[1][];
		unityObjectPoints [0] = new Vector3[numPoints];
		unityImagePoints = new Vector2[1][];
		unityImagePoints [0] = new Vector2[numPoints];
		objectPoints = new MCvPoint3D32f[1][];
		objectPoints [0] = new MCvPoint3D32f[numPoints];
		imagePoints = new PointF[1][];
		imagePoints [0] = new PointF[numPoints];

		imageSize = new Size (Screen.width, Screen.height);
		intrinsics = new IntrinsicCameraParameters (); 

		//Settings based on Mapamok default settings
		calibrationType = CALIB_TYPE.CV_CALIB_USE_INTRINSIC_GUESS 
				| CALIB_TYPE.CV_CALIB_FIX_PRINCIPAL_POINT //required to work properly !!
				| CALIB_TYPE.CV_CALIB_FIX_ASPECT_RATIO  
				| CALIB_TYPE.CV_CALIB_FIX_K1
				| CALIB_TYPE.CV_CALIB_FIX_K2
				| CALIB_TYPE.CV_CALIB_FIX_K3
				| CALIB_TYPE.CV_CALIB_FIX_K4
				| CALIB_TYPE.CV_CALIB_FIX_K5
				| CALIB_TYPE.CV_CALIB_ZERO_TANGENT_DIST
				;

		termCriteria = new MCvTermCriteria();
		
		//used for CV Matrix to unity camera position
		
		setPointsFromObject (calibObject);
	}
	
	void setPointsFromObject(GameObject o)
	{
		Mesh m = o.GetComponent<MeshFilter> ().sharedMesh;
		for (int i=0; i<numPoints; i++) {
			Vector3 op = calibObject.transform.TransformPoint(m.vertices[i]);
			unityObjectPoints[0][i] = op;
			
			objectPoints[0][i] = new MCvPoint3D32f(op.x,op.y,op.z);
			
			Vector2 sp = GetComponent<Camera>().WorldToScreenPoint(op);
			unityImagePoints[0][i] = sp;
			
			imagePoints[0][i] = new PointF(sp.x,sp.y);
		}
	}
	
	
	void updateImagePoints()
	{
		for (int i=0; i<numPoints; i++) {
			
			imagePoints[0][i] = new PointF(unityImagePoints[0][i].x,unityImagePoints[0][i].y);
		}
	}

	// Update is called once per frame
	void Update () {
		
		
		if (autoCalibrate) {
			calibrate ();
		}
		
		if(Input.GetKeyDown(KeyCode.A)) autoCalibrate = !autoCalibrate;
		
		if(Input.GetMouseButtonDown(0))
		{
			selectedTargetIndex = getClosestTargetIndex();
		}else if(Input.GetMouseButtonUp(0))
		{
			selectedTargetIndex = -1;
		}
		
		if(selectedTargetIndex != -1)
		{
			unityImagePoints[0][selectedTargetIndex] = Input.mousePosition;
			updateImagePoints();
		}
	}
	
	public void calibrate()
	{
		setIntrinsics ();
		calibrationError = CameraCalibration.CalibrateCamera (objectPoints, imagePoints, imageSize, intrinsics, calibrationType, termCriteria, out extrinsics);
		
		cvExtrinsics = convertExtrinsics(extrinsics[0].ExtrinsicMatrix);
		cvIntrinsics = convertIntrinsics(intrinsics.IntrinsicMatrix);
		
		updateCameraParams();
	}
	

	void OnGUI()
	{
		for (int i=0; i<numPoints; i++) {
			Vector2 p = unityImagePoints[0][i];
			p.y = Screen.height - p.y;
			Texture tex = (i==selectedTargetIndex)?overTex:targetTex;
			GUI.DrawTexture(new Rect(p.x-targetTex.width/2,p.y-targetTex.height/2,targetTex.width,targetTex.height),tex);
			GUI.Label(new Rect(p.x-targetTex.width/2,p.y-targetTex.height/2-20,50,50),i.ToString());
		}
		
		//GUI.TextField(new Rect(10,10,300,150),camera.worldToCameraMatrix.ToString());
		//GUI.TextField(new Rect(10,160,300,150),cvExtrinsics.ToString());
		
		GUI.Label(new Rect(10,10,200,50),"Error :"+calibrationError);
	}
	
	public void updateCameraParams()
	{
	
		//Vector3 zero = new Vector3(cvExtrinsics[3,0],-cvExtrinsics[3,1],cvExtrinsics[3,2]);
		//Quaternion q = Quaternion.LookRotation(cvExtrinsics.GetColumn(2), cvExtrinsics.GetColumn(1));
		//Quaternion.Inverse(q);
		//To convert to camera position when all is clean
		
		//camera.transform.position = Vector3.zero;
		//camera.transform.rotation = q;
		//Vector3 newT = camera.transform.TransformPoint(zero);
		//camera.transform.position = newT;
		//camera.transform.Rotate (Vector3.up,180);
		
		//camera.transform.position =extrinsics[0].TranslationVector
		GetComponent<Camera>().projectionMatrix = cvIntrinsics;
		GetComponent<Camera>().worldToCameraMatrix = cvExtrinsics;
	}

	//Utils
	
	//UI Util
	int getClosestTargetIndex()
	{
		float dist = Screen.width;
		int index = -1;
		
		for(int i=0;i<numPoints;i++)
		{
			float newDist = Vector2.Distance(Input.mousePosition,unityImagePoints[0][i]);
			if(newDist < dist)
			{
				dist = newDist;
				index = i;
			}
		}
		
		return index;
	} 
	
	
	
	//CV Util

	public void setImagePoints (Vector2[] points, int[] correspondance)
	{
		if(points.Length != numPoints)
		{
			Debug.LogError("Different number of points !");
			return;
		}
		
		Debug.Log ("set Image points !");
		
		for(int i=0;i<numPoints;i++)
		{
			Vector2 sp = points[i];
			unityImagePoints[0][correspondance[i]] = new Vector2(sp.x,Screen.height-sp.y);
			imagePoints[0][correspondance[i]] = new PointF(sp.x,sp.y);
		}
	}	
	
	
	void setIntrinsics()
	{
		double aov = GetComponent<Camera>().fieldOfView;
		double f = imageSize.Width * Mathf.Deg2Rad * aov; // i think this is wrong, but it's optimized out anyway
		Vector2 c =  new Vector2(imageSize.Width/2,imageSize.Height/2);
 
		intrinsics.IntrinsicMatrix[0,0] = f;
		intrinsics.IntrinsicMatrix[0,1] = 0;
		intrinsics.IntrinsicMatrix[0,2] = c.x;
		intrinsics.IntrinsicMatrix[1,0] = 0;
		intrinsics.IntrinsicMatrix[1,1] = f;
		intrinsics.IntrinsicMatrix[1,2] = c.y;
		intrinsics.IntrinsicMatrix[2,0] = 0;
		intrinsics.IntrinsicMatrix[2,1] = 0;
		intrinsics.IntrinsicMatrix[2,2] = 1;
	}
	
	public Matrix4x4 convertIntrinsics(Matrix<double> mat)
	{
		Matrix4x4 cm = CVMatToMat4x4(mat);
		float far = GetComponent<Camera>().farClipPlane;
		float near =GetComponent<Camera>().nearClipPlane;
		
		Matrix4x4 m = new Matrix4x4();
		m[0,0] = cm[0,0] / cm[0,2];
		m[1,1] = cm[1,1] / cm[1,2];
		m[2,2] = -(far+near)/(far-near);
		m[2,3] = -(2*far*near)/(far-near);
		m[3,2] = -1;
		
		return m;
	}
	
	public Matrix4x4 convertExtrinsics(Matrix<double> mat)
	{
		Matrix4x4 m = CVMatToMat4x4(mat);
		
		//m = m.transpose;
		
		//Invert some signs to conform to unity matrix
		m[2,0] = -m[2,0];
		m[2,1] = -m[2,1];
		m[2,2] = -m[2,2];
		m[2,3] = -m[2,3];
	
		m[3,3] = 1;
		
		return m;
	}

	public Matrix4x4 CVMatToMat4x4(Matrix<double> mat)
	{
		Matrix4x4 m = new Matrix4x4 ();
		for (int i=0; i<mat.Rows; i++) {
			for (int j=0; j<mat.Cols; j++) {
				m [i, j] =(float)mat[i, j];
			}
		}
		
		return m;
	}
}
