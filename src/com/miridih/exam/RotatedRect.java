package com.miridih.exam;

import java.util.*;
import java.io.*;

/**
 * 회전 사각형 프로그램 스켈레톤
 * */
public class RotatedRect implements Solver {
	
	public static void main(String[] args) {
		//Stock이라 되어 있는 부분을 수정했습니다.
        try {
			new RotatedRect().solve(new FileInputStream("C:/Users/Administrator/Downloads/TestFile/회전사각형-input.txt"), System.out);
		} catch (FileNotFoundException e) {
			// TODO Auto-generated catch block
			e.printStackTrace();
		}        
    }

	@Override
	public void solve(InputStream in, PrintStream out) {
		// TODO Auto-generated method stub
		try {
			Reader reader = new InputStreamReader(in);
			BufferedReader br = new BufferedReader(reader);
			String firstLine = br.readLine();
			int testCase = Integer.parseInt(firstLine);
//			out.println("testCase : " + testCase);
			while(testCase!=0) {
				String[] spaceChecker = br.readLine().split(" ");
				//중심점 좌표
				int x1 = Integer.parseInt(spaceChecker[0]);
				int y1 = Integer.parseInt(spaceChecker[1]);
				//너비 높이
				int width = Integer.parseInt(spaceChecker[2]);
				int height = Integer.parseInt(spaceChecker[3]);
				//각도
				int angle = Integer.parseInt(spaceChecker[4]);
				//래디안 좌표(시계방향)
				double radiansClockAngle = Math.toRadians(angle);
				//래디안 좌표(반시계방향)
				double radiansClockAngleBack = Math.toRadians(360-angle);
//				System.out.printf("좌표 : (%d,%d), 길이 : %d, 높이 : %d, 각도 %d도 \n",x1,y1,width,height,angle);
				//오른쪽 끝 좌표
				int x3 = x1+width;
				int y3 = y1+height;
//				System.out.printf("끝 좌표 : (%d,%d)\n",x3,y3);
				//회전후 좌표 계산 :  회전후 x좌표(x2) = (회전하는 x좌표-중심점 x좌표)*cos a - (회전하는 y좌표-중심점 y좌표)*sin a + 중심점 x좌표
				//회전 후 y좌표(y2) = (회전하는 x좌표-중심점 x좌표) * sin a + (회전하는 y좌표-중심점 y좌표) * cos a + 중심점 y좌표
				double x2 = ((x3-x1)*Math.cos(radiansClockAngle) - (y3-y1)*Math.sin(radiansClockAngle)+x1);
				double y2 = ((x3-x1)*Math.sin(radiansClockAngle) + (y3-y1)*Math.cos(radiansClockAngle)+y1);
//				System.out.printf("회전 후 끝 좌표 : (%f,%f)\n",x2,y2);
				//회전 후 C-rect 중심점 좌표 계산(현재 중심점 + 바뀐 중심점)
				double cx1 = (x1+x2)/2;
				double cy1 = (y1+y2)/2;
//				System.out.printf("회전 후 중심축 좌표 : (%f,%f)\n",cx1,cy1);
				//이후 현재 중심점을 기준으로 역방향 회전시켜 왼쪽 끝을 찾음.(좌표의 소숫점을 없애기 위해 반올림)
				long cx2 = Math.round(((x1-cx1)*Math.cos(radiansClockAngleBack) - (y1-cy1)*Math.sin(radiansClockAngleBack))+cx1);
				long cy2 = Math.round(((x1-cx1)*Math.sin(radiansClockAngleBack) + (y1-cy1)*Math.cos(radiansClockAngleBack))+cy1);
//				System.out.printf("회전 후 왼쪽 좌표? : (%d,%d)\n\n",cx2,cy2);
				out.printf("%d %d\n",cx2,cy2);
				testCase--;
				
			}
		} catch (IOException e) {
			// TODO Auto-generated catch block
			e.printStackTrace();
		} finally {
			try {
				if(in != null) {
					in.close();
				}
				if(out != null) {
					out.close();
				}
			} catch (IOException e) {
				// TODO Auto-generated catch block
				e.printStackTrace();
			}
		}
	}

}
